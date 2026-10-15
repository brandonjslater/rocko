using System;
using Rocko;

internal static class Program
{
    private const double Mass = 1025, Radius = 0.288, Ratio = 4.170148 * 3.667;

    //
    // @description - Run deterministic checks against the exact C# solver shipped in the game
    // @return {Void} - Prints results or throws on a regression
    //
    private static void Main()
    {
        foreach (double dt in new[] { 0.005, 0.01, 0.02 })
            foreach (bool assist in new[] { true, false }) Run(assist, dt, 1, 1);
        Run(true, 0.01, -1, 1);
        Run(true, 0.01, 1, 0.6);
        Recover("assist CSV 183704", 589.9, 8.198, new[] {42.655,40.699,255.557,257.145}, 2.33/0.737562149);
        Recover("manual CSV 183709", 1468, 5.079, new[] {115.112,115.100,167.152,167.152}, 68.73/0.737562149);
        Shift();
        Limits();
        PedalAndGrip();
        UnifiedTires();
        foreach (double dt in new[] { 0.005, 0.01, 0.02 }) ContactDamping(dt);
        ControllerChecks();
        Console.WriteLine("PASS: shared C# drivetrain checks. Unity suspension/cornering integration is not exercised here.");
    }

    // @description - Check analog precision, simultaneous pedals and steering recovery at multiple tick rates
    // @return {Void} - Throws if controller response violates its contract
    private static void ControllerChecks()
    {
        if (ControllerResponse.Shape(0.07, 0.08, 1.6) != 0 ||
            ControllerResponse.Shape(1, 0.08, 1.6) != 1 ||
            ControllerResponse.Shape(-1, 0.08, 1.6) != -1)
            throw new Exception("Controller deadzone/endpoints failed");
        double previous = 0;
        for (int i = 0; i <= 100; i++)
        {
            double value = ControllerResponse.Shape(i / 100.0, 0.08, 1.6);
            if (value < previous || Math.Abs(value + ControllerResponse.Shape(-i / 100.0, 0.08, 1.6)) > 1e-12)
                throw new Exception("Controller monotonicity/symmetry failed");
            previous = value;
        }
        if (ControllerResponse.Shape(0.5, 0.08, 1.6) >= 0.4 ||
            ControllerResponse.Shape(0.5, 0.02, 1.2) <= 0 ||
            ControllerResponse.Shape(0.5, 0.02, 1) <= 0)
            throw new Exception("Fine control or simultaneous pedals failed");
        foreach (double dt in new[] { 0.005, 0.01, 0.02 })
        {
            double steering = 1;
            for (int i = 0; i < (int)Math.Round(0.2 / dt); i++)
                steering = ControllerResponse.Steering(steering, -1, dt, 4.5, 8);
            if (Math.Abs(steering - (-0.3375)) > 1e-9)
                throw new Exception("Steering reversal depends on tick rate");
            double centered = 1;
            for (int i = 0; i < (int)Math.Round(0.2 / dt); i++)
                centered = ControllerResponse.Steering(centered, 0, dt, 4.5, 8);
            if (centered != 0) throw new Exception("Steering does not center promptly");
        }
        Console.WriteLine("Controller: deadzone, endpoints, symmetry, precision, pedals and tick-independent reversal PASS");
    }

    //
    // @description - Construct a flat-road contact fixture with a front-heavy static load distribution
    // @param {double} grip - Tire surface multiplier
    // @return {LongitudinalDrivetrain} - Initialized game solver with four grounded wheels
    //
    private static LongitudinalDrivetrain Create(double grip)
    {
        var model = new LongitudinalDrivetrain();
        model.Reset(900);
        for (int i = 0; i < 4; i++)
        {
            model.NormalLoad[i] = Mass * 9.80665 * (i < 2 ? 0.60 : 0.40) / 2;
            model.Grip[i] = grip * 1.12;
            model.WheelInertia[i] = 0.5 * 16 * Radius * Radius;
            for (int j = 0; j < 4; j++) model.DirectionDot[i * 4 + j] = 1;
        }
        return model;
    }

    //
    // @description - Interpolate the baseline crank torque anchors used by Rocko
    // @param {double} rpm - Engine RPM
    // @return {double} - Full-load net crank torque in Nm
    //
    private static double Curve(double rpm)
    {
        double[] x = { 0, 900, 1500, 2500, 3500, 4600, 5500, 6100, 6500, 6800, 7200 };
        double[] y = { 0, 72, 105, 132, 151, 165, 163, 102000 / (6100 * Math.PI / 30), 142, 123, 0 };
        for (int i = 1; i < x.Length; i++)
            if (rpm <= x[i]) return y[i-1] + (y[i]-y[i-1]) * (rpm-x[i-1]) / (x[i]-x[i-1]);
        return 0;
    }

    //
    // @description - Move a control toward its target with a bounded change
    // @param {double} value - Previous control value
    // @param {double} target - Desired value
    // @param {double} step - Maximum change
    // @return {double} - Updated value
    //
    private static double Move(double value, double target, double step)
    {
        return value + Math.Sign(target-value) * Math.Min(step, Math.Abs(target-value));
    }

    //
    // @description - Exercise a launch, sustained lock, engine braking, pedal release, and service braking
    // @param {bool} assist - Use automatic launch clutch pressure or a one-second manual pedal release
    // @param {double} dt - Outer physics timestep
    // @param {int} direction - Forward or reverse gear sign
    // @param {double} grip - Surface grip multiplier
    // @return {Void} - Throws if motion, sustained synchronization, or braking fails
    //
    private static void Run(bool assist, double dt, int direction, double grip)
    {
        var model = Create(grip);
        double overallRatio=direction<0?3.455*3.667:Ratio;
        double speed=0, throttle=0, pressure=0, cruise=-1, maxGap=0, brakeStart=-1, coastStart=-1;
        double coastSpeed=0, brakePedal=0, releasedClutch=0;
        bool launching=true, sawLock=false, sawPedalRelease=false;
        for (double time=0; time<20; time+=dt)
        {
            if (cruise < 0 && speed * direction >= 5) cruise=time;
            double sinceCruise=cruise < 0 ? -1 : time-cruise;
            double input=sinceCruise >= 2.5 ? 0 : sinceCruise >= 0 ? 0.3 : 1;
            double brakeInput=sinceCruise >= 4 ? 1 : 0;
            brakePedal=brakeInput<brakePedal?brakeInput:Move(brakePedal,brakeInput,dt*4);
            double brake=DrivingFeel.BrakeDemand(brakePedal);
            if (sinceCruise >= 2.5 && coastStart<0) { coastStart=time; coastSpeed=speed*direction; }
            if (sinceCruise >= 4 && brakeStart<0) brakeStart=time;
            double pedal=assist ? 0 : Math.Max(0, 1-(time-0.3));
            if (sinceCruise >= 3.5) pedal=1;
            throttle=Move(throttle,input,dt*4);
            double rpm=model.EngineOmega*30/Math.PI;
            double shaft=(model.WheelOmega[0]+model.WheelOmega[1])*0.5*overallRatio*direction*30/Math.PI;
            double fuel=time<0.24 || rpm>6800 ? 0 : throttle;
            double net=Curve(rpm)*fuel-(1-fuel)*(12+rpm*0.003);
            if (rpm<1000 && fuel<0.2) net+=Math.Clamp((900-rpm)*0.6+16,0,80);
            double targetRelease=1-pedal;
            releasedClutch=targetRelease<releasedClutch?targetRelease:Move(releasedClutch,targetRelease,dt*3);
            double engagement=time<0.24 ? 0 : assist?1-pedal:DrivingFeel.ClutchBite(releasedClutch);
            if (assist && engagement>0)
            {
                double roadRPM=Math.Abs(speed*overallRatio)*30/(Math.PI*Radius);
                if (roadRPM<1000 && shaft<1000) launching=true;
                else if (roadRPM>=1800) launching=false;
                if (launching)
                {
                    double slip=Math.Max(Math.Abs(model.Slip[0]),Math.Abs(model.Slip[1]));
                    double capacity=Math.Max(0,net+(rpm-(1200+1400*throttle))*0.15);
                    double launch=Math.Clamp(capacity/260,0,1)/(1+Math.Max(0,slip-0.08)*10);
                    double completion=Math.Clamp((roadRPM-1000)/800,0,1);
                    engagement*=launch+(1-launch)*completion;
                }
                if (brake>0.2 && shaft<1200) engagement=0;
            }
            pressure=Move(pressure,engagement,dt*(engagement<pressure?20:3));
            for (int i=0;i<4;i++)
            {
                double frontLoad=0.60+0.16*brake;
                model.NormalLoad[i]=Mass*9.80665*(i<2?frontLoad:1-frontLoad)/2;
                model.ContactSpeed[i]=speed;
                model.BrakeTorque[i]=brake*(i<2?1000:320);
            }
            model.Step(dt,net,0.28,260*pressure,overallRatio*direction,0.88,Mass,Radius);
            double impulse=0;
            foreach(double value in model.ForceImpulse) impulse+=value;
            double rolling=-Math.Sign(speed)*Math.Min(0.013*Mass*9.80665,Math.Abs(speed)*Mass/dt);
            speed+=(impulse+(rolling-0.5*1.225*0.62*Math.Abs(speed)*speed)*dt)/Mass;
            double gap=Math.Abs(model.EngineOmega-(model.WheelOmega[0]+model.WheelOmega[1])*0.5*overallRatio*direction)*30/Math.PI;
            bool locked=pressure>0.95 && gap<80;
            sawLock |= locked;
            if (sinceCruise>=0.5 && sinceCruise<2.5)
            {
                maxGap=Math.Max(maxGap,gap);
                Check(locked,"Sustained clutch lock",assist,dt,time,gap);
            }
            if (sinceCruise>=3.6) sawPedalRelease |= pressure<0.01 && Math.Abs(model.ClutchTorque)<0.001;
            if (brakeStart>=0 && time-brakeStart>3)
            {
                Check(Math.Abs(speed)<0.45,"Service brake stop",assist,dt,time,speed);
                break;
            }
            Check(!double.IsNaN(speed) && model.EngineOmega*30/Math.PI>440,"Finite running engine",assist,dt,time,model.EngineOmega*30/Math.PI);
        }
        Check(cruise>=0 && sawLock && sawPedalRelease && brakeStart>=0,"Scenario completes",assist,dt,0,speed);
        Console.WriteLine($"PASS mode={(assist?"assist":"manual")} dt={dt} direction={direction} grip={grip}: 5m/s at {cruise:F2}s, max cruise gap {maxGap:F6} RPM, final speed {speed:F3}m/s");
    }

    //
    // @description - Verify neutral, open clutch, tire force bounds, and airborne drive-wheel behavior
    // @return {Void} - Throws if a physical boundary condition fails
    //
    private static void Limits()
    {
        var model=Create(1);
        model.Step(0.01,100,0.28,260,0,0.88,Mass,Radius);
        Check(model.AxleTorque==0 && model.ClutchTorque==0,"Neutral torque",true,0.01,0,model.AxleTorque);
        model.Step(0.01,100,0.28,0,Ratio,0.88,Mass,Radius);
        Check(model.ClutchTorque==0,"Open clutch torque",true,0.01,0,model.ClutchTorque);
        for(int i=0;i<4;i++) model.NormalLoad[i]=0;
        model.Step(0.1,100,0.28,260,Ratio,0.88,Mass,Radius);
        foreach(double impulse in model.ForceImpulse) Check(impulse==0,"Airborne force",true,0.01,0,impulse);
        Check(model.WheelOmega[0]>0,"Airborne wheel rotation",true,0.01,0,model.WheelOmega[0]);
        for(double slip=-2;slip<2;slip+=0.01) Check(Math.Abs(LongitudinalDrivetrain.TireCoefficient(slip))<=1.03001,"Tire force cap",true,0.01,0,slip);
    }

    //
    // @description - Seed the exact shipped solver from a failed Unity CSV and require recovery under contact load
    // @param {string} label - Recording identifier
    // @param {double} rpm - Recorded engine speed
    // @param {double} speed - Recorded chassis speed in m/s
    // @param {double[]} wheels - Four recorded wheel speeds in RPM
    // @param {double} net - Recorded net crank torque in Nm
    // @return {Void} - Requires continuous synchronization during the final second
    //
    private static void Recover(string label,double rpm,double speed,double[] wheels,double net)
    {
        var model=Create(1);
        model.Reset(rpm);
        for(int i=0;i<4;i++) model.WheelOmega[i]=wheels[i]*Math.PI/30;
        double maxGap=0;
        for(int step=0;step<300;step++)
        {
            for(int i=0;i<4;i++) model.ContactSpeed[i]=speed;
            model.Step(0.01,net,0.28,260,Ratio,0.88,Mass,Radius);
            foreach(double impulse in model.ForceImpulse) speed+=impulse/Mass;
            double gap=Math.Abs(model.EngineOmega-(model.WheelOmega[0]+model.WheelOmega[1])*Ratio/2)*30/Math.PI;
            if(step>=200) { maxGap=Math.Max(maxGap,gap); Check(gap<80,label,true,0.01,step*0.01,gap); }
        }
        Console.WriteLine($"PASS failure-seeded recovery {label}: last-second max gap {maxGap:F6} RPM");
    }

    //
    // @description - Change from first to second with an open clutch and reacquire synchronization
    // @return {Void} - Requires zero torque while open and sustained lock in second
    //
    private static void Shift()
    {
        var model=Create(1);
        double speed=8;
        model.EngineOmega=speed/Radius*Ratio;
        for(int i=0;i<4;i++) model.WheelOmega[i]=speed/Radius;
        for(int step=0;step<400;step++)
        {
            double ratio=step<100?Ratio:2.453028*3.667;
            double capacity=step>=100 && step<124?0:260;
            for(int i=0;i<4;i++) model.ContactSpeed[i]=speed;
            model.Step(0.01,capacity==0?-20:80,0.28,capacity,ratio,0.88,Mass,Radius);
            foreach(double impulse in model.ForceImpulse) speed+=impulse/Mass;
            if(capacity==0) Check(model.ClutchTorque==0,"Shift disengages",true,0.01,step*0.01,model.ClutchTorque);
            if(step>200)
            {
                double gap=Math.Abs(model.EngineOmega-(model.WheelOmega[0]+model.WheelOmega[1])*ratio/2)*30/Math.PI;
                Check(gap<80,"Second gear lock",true,0.01,step*0.01,gap);
            }
        }
        Console.WriteLine("PASS first-to-second shift and sustained lock");
    }

    //
    // @description - Check pedal endpoints, monotonic bite, and steering travel
    // @return {Void} - Throws if pedal or steering authority is lost
    //
    private static void PedalAndGrip()
    {
        Check(DrivingFeel.SteeringLimit(0,13)==33 && DrivingFeel.SteeringLimit(30,13)<6,
            "Parking authority and reduced high-speed steering", false, 0, 0, 0);
        Check(DrivingFeel.SteeringLimit(-20,13)==DrivingFeel.SteeringLimit(20,13),
            "Reverse steering symmetry", false, 0, 0, 0);
        double previous = 0;
        for (int i = 0; i <= 100; i++)
        {
            double x = i / 100.0;
            double bite = DrivingFeel.ClutchBite(x);
            Check(bite >= previous && bite <= 1, "Monotonic bite", false, 0, x, bite);
            previous = bite;

        }
        Check(DrivingFeel.ClutchBite(0) == 0 && DrivingFeel.ClutchBite(1) == 1,
            "Clutch endpoints", false, 0, 0, 0);
        Check(DrivingFeel.BrakeDemand(0) == 0 && DrivingFeel.BrakeDemand(1) == 1 && DrivingFeel.BrakeDemand(0.5) < 0.5,
            "Progressive brakes retain full authority", false, 0, 0, 0);
        Console.WriteLine("PASS progressive pedals and steering limits");
    }

    //
    // @description - Verify shared grip, force direction, straight-line identity and load limits
    // @return {Void} - Throws if combined tires generate energy or exceed their friction budget
    //
    private static void UnifiedTires()
    {
        foreach (double speed in new[] { -20.0, -0.1, 0, 0.1, 20 })
        foreach (double slip in new[] { -1.0, -0.1, -0.03, 0, 0.03, 0.1, 1 })
        foreach (double lateral in new[] { -5.0, -0.1, 0, 0.1, 5 })
        {
            LongitudinalDrivetrain.CombinedForce(slip, lateral, speed, 3000, 1.12, 1.08, out double fx, out double fy);
            Check(Math.Sqrt(fx * fx + fy * fy) <= 1.03 * 3000 * 1.12 + 1e-8,
                "Combined grip limit", false, 0, speed, fx);
            Check(fx * (-slip) + fy * lateral <= 1e-8, "Contact dissipates slip", false, 0, speed, fy);
            if (lateral == 0)
                Check(Math.Abs(fx - LongitudinalDrivetrain.TireCoefficient(slip) * 3000 * 1.12) < 1e-8,
                    "Straight-line curve preserved", false, 0, speed, fx);
        }
        LongitudinalDrivetrain.CombinedForce(0, 1.5, 20, 3000, 1, 1, out _, out double coast);
        LongitudinalDrivetrain.CombinedForce(0.3, 1.5, 20, 3000, 1, 1, out _, out double power);
        LongitudinalDrivetrain.CombinedForce(-0.3, 1.5, 20, 3000, 1, 1, out _, out double braking);
        Check(Math.Abs(power) < Math.Abs(coast) && Math.Abs(braking - power) < 1e-9,
            "Drive and braking share cornering grip", false, 0, 0, power);
        LongitudinalDrivetrain.CombinedForce(1, 5, 0, 0, 1, 1, out double unloadedX, out double unloadedY);
        Check(unloadedX == 0 && unloadedY == 0, "Airborne has no tire force", false, 0, 0, 0);
        Console.WriteLine("PASS combined tires: grip bound, dissipation, reverse/zero speed, airborne, straight-line identity");
    }

    //
    // @description - Integrate a four-contact chassis with translation and yaw through a lateral slide
    // @param {double} dt - Outer simulation timestep
    // @return {Void} - Asserts contact-matrix integration damps slip and never creates kinetic energy
    //
    private static void ContactDamping(double dt)
    {
        var model = Create(1);
        model.UseContactResponse = true;
        model.EngineOmega = 0;
        double[] x = { -0.695, 0.695, -0.695, 0.695 };
        double[] z = { 1.2, 1.2, -1.2, -1.2 };
        double lateral = 2, forward = 15, yaw = 0.3, yawInertia = 1450;
        for (int i = 0; i < 4; i++) model.WheelOmega[i] = (forward - yaw * x[i]) / Radius;
        for (int i = 0; i < 8; i++)
        for (int j = 0; j < 8; j++)
        {
            double armI = i < 4 ? -x[i] : z[i - 4];
            double armJ = j < 4 ? -x[j] : z[j - 4];
            model.ContactResponse[i * 8 + j] = ((i < 4) == (j < 4) ? 1 / Mass : 0)
                + armI * armJ / yawInertia;
        }
        double previousEnergy = double.PositiveInfinity;
        for (int tick = 0; tick < (int)Math.Round(3 / dt); tick++)
        {
            for (int i = 0; i < 4; i++)
            {
                model.ContactSpeed[i] = forward - yaw * x[i];
                model.LateralContactSpeed[i] = lateral + yaw * z[i];
            }
            model.Step(dt, 0, 0.28, 0, 0, 1, Mass, Radius);
            for (int i = 0; i < 4; i++)
            {
                forward += model.ForceImpulse[i] / Mass;
                lateral += model.LateralImpulse[i] / Mass;
                yaw += (-x[i] * model.ForceImpulse[i] + z[i] * model.LateralImpulse[i]) / yawInertia;
                Check(Math.Abs(model.ContactSpeed[i]) < 100, "Finite contact state", false, dt, tick * dt, model.ContactSpeed[i]);
            }
            double energy = 0.5 * Mass * (forward * forward + lateral * lateral) + 0.5 * yawInertia * yaw * yaw;
            for (int i = 0; i < 4; i++) energy += 0.5 * model.WheelInertia[i] * model.WheelOmega[i] * model.WheelOmega[i];
            Check(energy <= previousEnergy + 1e-6, "Combined contact energy cannot increase", false, dt, tick * dt, energy - previousEnergy);
            previousEnergy = energy;
        }
        Check(Math.Abs(lateral) < 0.02 && Math.Abs(yaw) < 0.02,
            "Lateral and yaw motion settle", false, dt, 3, lateral);
        Console.WriteLine($"PASS contact matrix dt={dt}: lateral={lateral:F6} m/s, yaw={yaw:F6} rad/s");
    }

    //
    // @description - Fail a regression with reproducible fixture context
    // @param {bool} passed - Assertion result
    // @param {string} label - Check name
    // @param {bool} assist - Fixture clutch mode
    // @param {double} dt - Fixture outer timestep
    // @param {double} time - Fixture elapsed time
    // @param {double} value - Measured value
    // @return {Void} - Throws on a failed assertion
    //
    private static void Check(bool passed,string label,bool assist,double dt,double time,double value)
    {
        if(!passed) throw new Exception($"{label}: assist={assist}, dt={dt}, t={time:F3}, value={value:F6}");
    }
}
