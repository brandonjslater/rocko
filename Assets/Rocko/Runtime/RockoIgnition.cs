using UnityEngine;

namespace Rocko
{
    public sealed class RockoIgnition : MonoBehaviour
    {
        public RockoVehicle car;
        public Collider Target;
        public Transform keyVisual;
        public bool KeyInserted { get; private set; }
        public bool Cranking { get; private set; }
        public bool MustRelease { get; private set; }
        public bool StarterEngaged { get { return Cranking && !car.EngineRunning && car.CanStartEngine; } }
        private float crankTime;

        //
        // @description - Insert the key on the first click, then permit a separate hold to crank
        // @return {Void} - Updates ignition state only while seated
        //
        public void Press()
        {
            if (!car.occupied || MustRelease) return;
            if (!KeyInserted)
            {
                KeyInserted = true; MustRelease = true;
                if (keyVisual != null) keyVisual.gameObject.SetActive(true);
                car.SetNotice("Key inserted · release, then hold Mouse 1 / R1 on the ignition to crank.");
                return;
            }
            if (car.EngineRunning) { car.SetNotice("Engine already running."); return; }
            if (!car.CanStartEngine) { car.SetNotice("Select neutral or hold the clutch to crank."); return; }
            Cranking = true; crankTime = 0;
        }

        //
        // @description - Advance cranking while held; require release after the engine catches
        // @param {bool} held - Whether the interaction button remains down
        // @param {float} dt - Scaled frame duration
        // @return {Void} - Starts the engine after sustained cranking or releases the starter
        //
        public void Tick(bool held, float dt)
        {
            if (!held || !car.occupied)
            {
                Cranking = false; MustRelease = false; crankTime = 0;
                if (keyVisual != null) keyVisual.localRotation = Quaternion.identity;
                return;
            }
            if (!Cranking) return;
            if (!car.CanStartEngine) { CancelHold(); return; }
            if (keyVisual != null) keyVisual.localRotation = Quaternion.Euler(0, 0, -35);
            crankTime += Mathf.Max(0, dt);
            if (!car.EngineRunning && crankTime >= 0.7f && car.StartEngine())
            {
                MustRelease = true;
                car.SetNotice("Engine started · release Mouse 1 / R1 to release the key.");
            }
        }

        //
        // @description - Release the starter safely on pause, loss of focus or leaving the seat
        // @return {Void} - Cancels the current hold without removing the key
        //
        public void CancelHold() { Cranking = false; crankTime = 0; MustRelease = true; }

        //
        // @description - Restore an empty ignition when resetting the vehicle
        // @return {Void} - Clears the key and starter state
        //
        public void ResetIgnition()
        {
            KeyInserted = Cranking = MustRelease = false; crankTime = 0;
            if (keyVisual != null) { keyVisual.gameObject.SetActive(false); keyVisual.localRotation = Quaternion.identity; }
        }
    }
}
