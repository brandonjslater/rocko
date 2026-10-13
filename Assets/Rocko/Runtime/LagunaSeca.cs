using System.Collections.Generic;
using UnityEngine;

namespace Rocko
{
    public sealed class LagunaSeca : MonoBehaviour
    {
        public Vector3[] Points { get; private set; }
        public float Length { get; private set; }
        public Vector3 StartPosition { get { return Points[0] + Vector3.up * 0.15f; } }
        public Quaternion StartRotation { get { return Quaternion.LookRotation(Points[1] - Points[0]); } }
        private Material asphalt, sand, white, red;
        // Map-space trace of the official racing-lines flyer; elevations are estimates in metres.
        private readonly Vector3[] anchors = {
            new Vector3(660,10,980), new Vector3(820,12,980), new Vector3(930,16,974),
            new Vector3(1030,10,937), new Vector3(1145,5,883), new Vector3(1173,5,862),
            new Vector3(1160,5,834), new Vector3(1120,5,810), new Vector3(1090,5,814),
            new Vector3(1040,5,852), new Vector3(970,5,886), new Vector3(880,5,908),
            new Vector3(843,5,901), new Vector3(820,5,876), new Vector3(789,6,771),
            new Vector3(755,7,678), new Vector3(762,7,651), new Vector3(784,7,633),
            new Vector3(930,9,578), new Vector3(1070,10,561), new Vector3(1160,12,550),
            new Vector3(1187,14,529), new Vector3(1185,16,507), new Vector3(1144,20,461),
            new Vector3(1035,29,384), new Vector3(951,34,327), new Vector3(924,34,320),
            new Vector3(810,41,317), new Vector3(687,49,335), new Vector3(600,55,346),
            new Vector3(551,59.86f,350), new Vector3(525,59.86f,365),
            new Vector3(527,56.86f,392), new Vector3(508,53.86f,412), new Vector3(460,48.86f,431),
            new Vector3(408,41.88f,434), new Vector3(365,33,460), new Vector3(347,26.6f,486),
            new Vector3(350,25,507), new Vector3(400,20,566), new Vector3(461,14,632),
            new Vector3(473,12,652), new Vector3(465,11,672), new Vector3(395,10,734),
            new Vector3(298,10,814), new Vector3(235,10,868), new Vector3(224,10,885),
            new Vector3(231,10,899), new Vector3(280,10,913), new Vector3(455,10,944)
        };

        //
        // @description - Generate the closed reference-layout circuit and its driveable runoff
        // @return {Void} - Builds road, shoulders, terrain and corner markers once
        //
        private void Awake()
        {
            asphalt = Geometry.Material("Laguna asphalt", new Color(.19f,.19f,.18f), .08f);
            sand = Geometry.Material("Laguna dry hills", new Color(.55f,.46f,.28f), .02f);
            white = Geometry.Material("Laguna white", new Color(.88f,.86f,.77f));
            red = Geometry.Material("Laguna curb red", new Color(.68f,.13f,.09f));
            var samples = new List<Vector3>();
            for (int i=0;i<anchors.Length;i++)
            {
                Vector3 a=anchors[(i+anchors.Length-1)%anchors.Length], b=anchors[i];
                Vector3 c=anchors[(i+1)%anchors.Length], d=anchors[(i+2)%anchors.Length];
                int steps=Mathf.Max(16,Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a,b),Mathf.Max(Vector3.Distance(b,c),Vector3.Distance(c,d)))));
                for(int j=0;j<steps;j++)
                {
                    float t=j/(float)steps;
                    float chord=Vector3.Distance(b,c);
                    Vector3 incoming=(c-a).normalized*chord, outgoing=(d-b).normalized*chord;
                    Vector3 p=(2*t*t*t-3*t*t+1)*b+(t*t*t-2*t*t+t)*incoming+(-2*t*t*t+3*t*t)*c+(t*t*t-t*t)*outgoing;
                    // Monotone height interpolation avoids overshooting the Corkscrew crest.
                    float ab=new Vector2(b.x-a.x,b.z-a.z).magnitude;
                    float bc=new Vector2(c.x-b.x,c.z-b.z).magnitude;
                    float cd=new Vector2(d.x-c.x,d.z-c.z).magnitude;
                    float m0=HeightSlope((b.y-a.y)/ab,(c.y-b.y)/bc)*bc;
                    float m1=HeightSlope((c.y-b.y)/bc,(d.y-c.y)/cd)*bc;
                    p.y=(2*t*t*t-3*t*t+1)*b.y+(t*t*t-2*t*t+t)*m0+(-2*t*t*t+3*t*t)*c.y+(t*t*t-t*t)*m1;
                    samples.Add(new Vector3(p.x-660,p.y,980-p.z));
                }
            }
            Points=samples.ToArray();
            float low=.1f,high=3;
            for(int iteration=0;iteration<30;iteration++)
            {
                float scale=(low+high)/2, length=0;
                for(int i=0;i<Points.Length;i++)
                {
                    Vector3 delta=Points[(i+1)%Points.Length]-Points[i];
                    delta.x*=scale;delta.z*=scale;length+=delta.magnitude;
                }
                if(length<3601.711872f) low=scale; else high=scale;
            }
            float factor=(low+high)/2;
            for(int i=0;i<Points.Length;i++) Points[i]=new Vector3(Points[i].x*factor+3000,Points[i].y,Points[i].z*factor);
            Length=0;
            for(int i=0;i<Points.Length;i++) Length+=Vector3.Distance(Points[i],Points[(i+1)%Points.Length]);
            // Wide offset ribbons fold across the road at tight downhill corners.
            // Keep shoulders local and adapt their width to each corner's curvature.
            Shoulders();
            Strip("Closed asphalt circuit",-5.5f,5.5f,0,asphalt,true,1);
            Strip("Left edge line",-5.35f,-5.20f,.015f,white,false,1);
            Strip("Right edge line",5.20f,5.35f,.015f,white,false,1);
            Terrain();
            int[] turns={2,6,12,16,21,26,29,31,33,37,41,46};
            string[] names={"1","2 / ANDRETTI","3","4","5","6","7 / CREST","8 / CORKSCREW","8A","9 / RAINEY","10","11"};
            for(int i=0;i<turns.Length;i++)
            {
                Vector3 target=new Vector3((anchors[turns[i]].x-660)*factor+3000,anchors[turns[i]].y,(980-anchors[turns[i]].z)*factor);
                int index=Nearest(target); Marker(index,"TURN "+names[i]);
                for(int k=-10;k<=10;k++)
                {
                    int n=(index+k*2+Points.Length)%Points.Length;
                    Vector3 right=Vector3.Cross(Vector3.up,Direction(n)).normalized;
                    for(int side=-1;side<=1;side+=2)
                    {
                        GameObject curb=Geometry.Box("Flush curb paint",transform,Points[n]+right*side*5.8f+Vector3.up*.01f,new Vector3(.6f,.02f,2),k%2==0?red:white);
                        curb.transform.rotation=Quaternion.LookRotation(Direction(n));
                    }
                }
            }
            GameObject line=Geometry.Box("Start finish line",transform,Points[0]+Vector3.up*.02f,new Vector3(11,.025f,1),white);
            line.transform.rotation=StartRotation;
            Marker(0,"LAGUNA SECA / START\nApproximate layout / F4 reset");
        }

        //
        // @description - Limit elevation tangents to prevent crest and valley overshoot
        // @param {float} before - Incoming elevation gradient
        // @param {float} after - Outgoing elevation gradient
        // @return {float} - Monotone shared tangent
        //
        private static float HeightSlope(float before,float after)
        {
            return before*after<=0 ? 0 : 2*before*after/(before+after);
        }

        //
        // @description - Return a periodic centerline tangent
        // @param {int} index - Sample index
        // @return {Vector3} - Normalized forward direction
        //
        public Vector3 Direction(int index)
        {
            return (Points[(index+1)%Points.Length]-Points[(index+Points.Length-1)%Points.Length]).normalized;
        }

        //
        // @description - Find the closest centerline sample for navigation and geometry checks
        // @param {Vector3} position - World position
        // @return {int} - Nearest sample index
        //
        public int Nearest(Vector3 position)
        {
            int result=0;float best=float.PositiveInfinity;
            for(int i=0;i<Points.Length;i++)
            {
                float distance=(Points[i]-position).sqrMagnitude;
                if(distance<best){best=distance;result=i;}
            }
            return result;
        }

        //
        // @description - Build a welded periodic ribbon with consistent upward winding
        // @param {string} label - Object name
        // @param {float} left - Left offset in metres
        // @param {float} right - Right offset in metres
        // @param {float} height - Vertical offset
        // @param {Material} material - Surface appearance
        // @param {bool} solid - Enable collision
        // @param {float} grip - Tire grip multiplier
        // @return {Void} - Adds one mesh and optional tire metadata
        //
        private void Strip(string label,float left,float right,float height,Material material,bool solid,float grip)
        {
            var vertices=new Vector3[Points.Length*2];var triangles=new int[Points.Length*6];
            for(int i=0;i<Points.Length;i++)
            {
                Vector3 across=Vector3.Cross(Vector3.up,Direction(i)).normalized;
                vertices[i*2]=Points[i]+across*left+Vector3.up*height;
                vertices[i*2+1]=Points[i]+across*right+Vector3.up*height;
                int n=i*2,next=((i+1)%Points.Length)*2,t=i*6;
                triangles[t]=n;triangles[t+1]=next;triangles[t+2]=n+1;
                triangles[t+3]=n+1;triangles[t+4]=next;triangles[t+5]=next+1;
            }
            GameObject road=Geometry.Mesh(label,transform,vertices,triangles,material,solid);
            if(solid){RoadSurface surface=road.AddComponent<RoadSurface>();surface.label=grip==1?"Laguna asphalt":"Laguna runoff";surface.grip=grip;surface.rollingResistance=grip==1?.013f:.04f;}
        }

        //
        // @description - Build narrow shoulders whose inner-corner width cannot fold over the road
        // @return {Void} - Adds separate left and right shoulder meshes
        //
        private void Shoulders()
        {
            for(int side=-1;side<=1;side+=2)
            {
                var vertices=new Vector3[Points.Length*2];var triangles=new int[Points.Length*6];
                for(int i=0;i<Points.Length;i++)
                {
                    int previous=(i+Points.Length-1)%Points.Length,next=(i+1)%Points.Length;
                    Vector3 right=Vector3.Cross(Vector3.up,Direction(i)).normalized;
                    Vector3 previousRight=Vector3.Cross(Vector3.up,Direction(previous)).normalized;
                    Vector3 nextRight=Vector3.Cross(Vector3.up,Direction(next)).normalized;
                    Vector3 travel=Points[next]-Points[previous];travel.y=0;
                    float contraction=-Vector3.Dot((nextRight-previousRight)*side,travel)/travel.sqrMagnitude;
                    float outer=contraction>0 ? Mathf.Min(8.5f, .98f/contraction) : 8.5f;
                    outer=Mathf.Max(5.5f,outer);
                    Vector3 inner=Points[i]+right*side*5.5f-Vector3.up*.02f;
                    Vector3 edge=Points[i]+right*side*outer-Vector3.up*.12f;
                    vertices[i*2]=side<0?edge:inner;vertices[i*2+1]=side<0?inner:edge;
                    int n=i*2,k=next*2,t=i*6;
                    triangles[t]=n;triangles[t+1]=k;triangles[t+2]=n+1;
                    triangles[t+3]=n+1;triangles[t+4]=k;triangles[t+5]=k+1;
                }
                GameObject shoulder=Geometry.Mesh("Narrow shoulder "+side,transform,vertices,triangles,sand,true);
                RoadSurface surface=shoulder.AddComponent<RoadSurface>();surface.label="Laguna shoulder";surface.grip=.6f;surface.rollingResistance=.04f;
            }
        }

        //
        // @description - Build a surrounding dry hillside mesh below the road and runoff
        // @return {Void} - Adds a collidable terrain surface
        //
        private void Terrain()
        {
            Vector3 min=Points[0],max=Points[0];
            foreach(Vector3 p in Points){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            const int count=100;var vertices=new Vector3[(count+1)*(count+1)];var triangles=new List<int>();
            for(int z=0;z<=count;z++) for(int x=0;x<=count;x++)
            {
                Vector3 p=new Vector3(Mathf.Lerp(min.x-150,max.x+150,x/(float)count),0,Mathf.Lerp(min.z-150,max.z+150,z/(float)count));
                float best=float.PositiveInfinity,y=0;
                for(int i=0;i<Points.Length;i+=4)
                {
                    float distance=new Vector2(p.x-Points[i].x,p.z-Points[i].z).sqrMagnitude;
                    if(distance<best){best=distance;y=Points[i].y;}
                }
                p.y=y-5-Mathf.Sqrt(best)*.03f;vertices[z*(count+1)+x]=p;
                if(x<count&&z<count){int n=z*(count+1)+x;triangles.AddRange(new[]{n,n+count+1,n+1,n+1,n+count+1,n+count+2});}
            }
            // Lower every cell that can touch the road corridor. Checking only the nearest
            // centerline height misses upper-hill triangles spanning a lower section of road.
            float cellX=(max.x-min.x+300)/count,cellZ=(max.z-min.z+300)/count;
            float clearanceRadius=9+Mathf.Sqrt(cellX*cellX+cellZ*cellZ);
            foreach(Vector3 point in Points)
            {
                int x0=Mathf.Max(0,Mathf.FloorToInt((point.x-clearanceRadius-(min.x-150))/cellX));
                int x1=Mathf.Min(count,Mathf.CeilToInt((point.x+clearanceRadius-(min.x-150))/cellX));
                int z0=Mathf.Max(0,Mathf.FloorToInt((point.z-clearanceRadius-(min.z-150))/cellZ));
                int z1=Mathf.Min(count,Mathf.CeilToInt((point.z+clearanceRadius-(min.z-150))/cellZ));
                for(int z=z0;z<=z1;z++) for(int x=x0;x<=x1;x++)
                {
                    int index=z*(count+1)+x;
                    vertices[index].y=Mathf.Min(vertices[index].y,point.y-.5f);
                }
            }
            GameObject ground=Geometry.Mesh("Estimated surrounding hills",transform,vertices,triangles.ToArray(),sand,true);
            RoadSurface surface=ground.AddComponent<RoadSurface>();surface.label="Dry hillside";surface.grip=.5f;surface.rollingResistance=.05f;
        }

        //
        // @description - Place a corner board facing approaching traffic
        // @param {int} index - Centerline sample
        // @param {string} text - Board text
        // @return {Void} - Creates an off-track landmark
        //
        private void Marker(int index,string text)
        {
            Vector3 right=Vector3.Cross(Vector3.up,Direction(index)).normalized;
            Transform sign=new GameObject(text).transform;sign.SetParent(transform,false);
            sign.position=Points[index]+right*9+Vector3.up*2;sign.rotation=Quaternion.LookRotation(Direction(index));
            Geometry.Box("Board",sign,Vector3.zero,new Vector3(7,1.6f,.1f),asphalt);
            Geometry.Label(text,sign,new Vector3(0,0,-.06f),.3f);
        }
    }
}
