using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            //This makes the program automatically run every 10 ticks.
        }

        string REMOTE_CONTROL_NAME = ""; //Set name for remote control to orient on,
                                         //leave blank to use first one found
        double CTRL_COEFF = 0.8; //Set lower if overshooting, set higher to respond quicker
        int LIMIT_GYROS = 999; //Set to the max number of gyros to use
                               //(Using less gyros than you have allows you to still steer while
                               // leveler is operating.)

        ////////////////////////////////////////////////////////////

        List<IMyThrust> allthrusters
         = new List<IMyThrust>();
        List<IMyThrust> fwdthrusters
         = new List<IMyThrust>();
        List<IMyThrust> bwdthrusters
         = new List<IMyThrust>();
        List<IMyThrust> upthrusters
         = new List<IMyThrust>();
        List<IMyThrust> downthrusters
         = new List<IMyThrust>();

        List<IMyCameraBlock> allcams = new List<IMyCameraBlock>();
        List<IMyCameraBlock> fwdcams = new List<IMyCameraBlock>();
        List<IMyCameraBlock> leftcams = new List<IMyCameraBlock>();
        List<IMyCameraBlock> rightcams = new List<IMyCameraBlock>();
        List<IMyCameraBlock> upcams = new List<IMyCameraBlock>();
        List<IMyCameraBlock> downcams = new List<IMyCameraBlock>();
        List<MyDetectedEntityInfo> camdata = new List<MyDetectedEntityInfo>();

        List<IMyGyro> gyros;
        IMyRemoteControl rc;
        IMyTextPanel lcd;
        IMyProgrammableBlock pb;
        IMyCameraBlock elevcam;
        IMyRadioAntenna antenna;

        List<MyWaypointInfo> allwps = new List<MyWaypointInfo>();
        List<float> coords = new List<float>();
        List<string> lcdtext = new List<string>();
        List<Vector3D> poscheck = new List<Vector3D>();

        bool wpcheck = false;
        bool firstraycast = true;

        string travelorder = "intravel";
        string mission = "None";

        Vector3D targetpos = new Vector3D();
        Vector3D currentpos = new Vector3D();
        Vector3D lastpos = new Vector3D();
        Vector3D campos = new Vector3D();

        double lastspeed = 0.0;
        double currentspeed = 0.0;
        double currentacc = 0.0;
        double elevation;
        double vertspeed = 0.0;
        double latspeed = 0.0;

        float mship = 0;
        float mtot = 0;

        float maxtfwd = 0;
        float maxtbwd = 0;
        float maxtup = 0;
        float maxtdown = 0;

        string droneid = "MIN-xxx";
        bool idchange = false;

        //For measuring time/ticks. Incremented at each run of Main.
        int tick = 0;


        public void Main(string arg, UpdateType updatesource)
        {

            Echo("Drone ID: " + droneid);
            Echo("Tick: " + tick);

            if (rc == null)
            {
                setup();
            }
            else if (arg == "setup")
            {
                setup();
            }

            lcdtext.Clear();

            //Current position, speed and acceleration.
            lastpos = currentpos;
            currentpos = rc.GetPosition();

            lastspeed = currentspeed;
            currentspeed = ((currentpos - lastpos).Length() * 6);
            //Echo("Speed: " + currentspeed.ToString() + " m/s");

            currentacc = (currentspeed - lastspeed) * 6;
            lcdtext.Add("Acc: " + currentacc + " m/s2");

            //If script is triggered by antenna.
            if ((updatesource & UpdateType.Antenna) != 0)
            {
                ParseTransmission(arg);
            }

            //Get GPS waypoint.
            if (!wpcheck)
            {
                targetpos = GetGPSWaypoint();
            }

            lcdtext.Add("Speed: " + currentspeed.ToString() + " m/s");

            //Level out gravity and rotate towards target.
            LevelGravity();
            FaceTarget();

            if (travelorder == "intravel")
            {
                //Travel to target.
                travelorder = Travel();
            }

            if (travelorder == "intravel")
            {
                //Echo("Travelling...");
                lcdtext.Add("Travelling...");
            }
            else if (travelorder == "stop")
            {
                //Echo("Arrived at target.");
                lcdtext.Add("Arrived at target.");
            }




            LCDPrint(lcdtext);

            if (tick >= 10)
            {
                UpdateMother();
                tick = 0;
            }
            else
            {
                tick++;
            }
        }

        public void setup()
        {



            var l = new List<IMyTerminalBlock>();

            rc = (IMyRemoteControl)GridTerminalSystem.GetBlockWithName(REMOTE_CONTROL_NAME);
            if (rc == null)
            {
                GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(l, x => x.CubeGrid == Me.CubeGrid);
                rc = (IMyRemoteControl)l[0];
            }

            antenna = (IMyRadioAntenna)GridTerminalSystem.GetBlockWithName("Antenna");
            if (antenna == null)
            {
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(l, x => x.CubeGrid == Me.CubeGrid);
                antenna = (IMyRadioAntenna)l[0];
            }

            lcd = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("LCD");
            if (lcd == null)
            {
                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(l, x => x.CubeGrid == Me.CubeGrid);
                lcd = (IMyTextPanel)l[0];
            }

            pb = (IMyProgrammableBlock)GridTerminalSystem.GetBlockWithName("Drone PB");
            if (pb == null)
            {
                GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(l, x => x.CubeGrid == Me.CubeGrid);
                pb = (IMyProgrammableBlock)l[0];
            }

            GridTerminalSystem.GetBlocksOfType<IMyGyro>(l, x => x.CubeGrid == Me.CubeGrid);
            gyros = l.ConvertAll(x => (IMyGyro)x);
            if (gyros.Count > LIMIT_GYROS)
                gyros.RemoveRange(LIMIT_GYROS, gyros.Count - LIMIT_GYROS);

            GridTerminalSystem.GetBlocksOfType<IMyThrust>(l, x => x.CubeGrid == Me.CubeGrid);
            allthrusters = l.ConvertAll(x => (IMyThrust)x);

            GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(l, x => x.CubeGrid == Me.CubeGrid);
            allcams = l.ConvertAll(x => (IMyCameraBlock)x);

            var controller = rc as IMyShipController;
            MyShipMass masses = controller.CalculateShipMass();
            mship = masses.BaseMass;
            mtot = masses.TotalMass;

            //rc orientation matrix.
            Matrix rcor;
            rc.Orientation.GetMatrix(out rcor);
            Vector3D rcbwd = rcor.Backward;
            Vector3D rcfwd = rcor.Forward;
            Vector3D rcleft = rcor.Left;
            Vector3D rcright = rcor.Right;
            Vector3D rcup = rcor.Up;
            Vector3D rcdown = rcor.Down;
            rcbwd.Normalize();
            rcfwd.Normalize();
            rcleft.Normalize();
            rcright.Normalize();
            rcup.Normalize();
            rcdown.Normalize();

            //Empty orientation matrix for thruster.
            Matrix tor;

            maxtfwd = 0;
            maxtbwd = 0;
            maxtup = 0;
            maxtdown = 0;

            for (int i = 0; i < allthrusters.Count; i++)
            {
                allthrusters[i].Orientation.GetMatrix(out tor);
                Vector3D thrustdir = tor.Forward;
                thrustdir.Normalize();

                if (rcbwd.Equals(thrustdir))
                {
                    fwdthrusters.Add(allthrusters[i]);
                    maxtfwd = maxtfwd + allthrusters[i].MaxThrust;
                }
                else if (rcfwd.Equals(thrustdir))
                {
                    bwdthrusters.Add(allthrusters[i]);
                    maxtbwd = maxtbwd + allthrusters[i].MaxThrust;
                }
                else if (rcup.Equals(thrustdir))
                {
                    downthrusters.Add(allthrusters[i]);
                    maxtdown = maxtdown + allthrusters[i].MaxThrust;
                }
                else if (rcdown.Equals(thrustdir))
                {
                    upthrusters.Add(allthrusters[i]);
                    maxtup = maxtup + allthrusters[i].MaxThrust;
                }
            }

            //Empty orientation matrix for camera.
            Matrix cor;

            for (int i = 0; i < allcams.Count; i++)
            {
                allcams[i].Orientation.GetMatrix(out cor);
                Vector3D camdir = cor.Forward;
                camdir.Normalize();

                if (rcfwd.Equals(camdir))
                {
                    fwdcams.Add(allcams[i]);
                }
                else if (rcleft.Equals(camdir))
                {
                    leftcams.Add(allcams[i]);
                }
                else if (rcright.Equals(camdir))
                {
                    rightcams.Add(allcams[i]);
                }
                else if (rcup.Equals(camdir))
                {
                    upcams.Add(allcams[i]);
                }
                else if (rcdown.Equals(camdir))
                {
                    downcams.Add(allcams[i]);
                }
            }

            elevcam = (IMyCameraBlock)GridTerminalSystem.GetBlockWithName("Elevation Camera");
            if (elevcam == null)
            {
                elevcam = downcams[0];
            }
            currentpos = rc.GetPosition(); //Get position for first speed calculation.
        }

        public Vector3D GetGPSWaypoint()
        {
            //Get waypoint from rc.
            rc.GetWaypointInfo(allwps);
            string wptext = allwps[0].ToString();
            Echo(wptext);

            //String pattern for parsing GPS-coordinates.
            string pattern = @":(-*\d*\.*\d*):(-*\d*\.*\d*):(-*\d*\.*\d*)";
            System.Text.RegularExpressions.Regex rgx = new System.Text.RegularExpressions.Regex(pattern);

            System.Text.RegularExpressions.MatchCollection matches = rgx.Matches(wptext);
            System.Text.RegularExpressions.Match match = null;

            //Echo("Matches = " + matches.Count.ToString());

            if (matches.Count > 0)
            {
                match = matches[0];

                for (int i = 0; i < match.Groups.Count; i++)
                {
                    float coord;
                    if (float.TryParse(match.Groups[i].ToString(), out coord))
                    {
                        coords.Add(coord);
                        Echo("Coord: " + coord.ToString());
                    }
                }
            }

            if (coords.Count >= 3)
            {
                targetpos.X = coords[0];
                targetpos.Y = coords[1];
                targetpos.Z = coords[2];

                //Echo("TargetPos: " + coords[0].ToString() + ", " + coords[1].ToString() + ", " + coords[2]);
            }

            wpcheck = true;
            return targetpos;


        }

        public void LevelGravity()
        {
            //Get orientation from rc
            Matrix or;
            rc.Orientation.GetMatrix(out or);
            Vector3D down = or.Down;

            Vector3D grav = rc.GetNaturalGravity();
            grav.Normalize();

            //Level to gravity.
            for (int i = 0; i < gyros.Count; ++i)
            {


                var g = gyros[i];



                g.Orientation.GetMatrix(out or);
                var localDown = Vector3D.Transform(down, MatrixD.Transpose(or));

                var localGrav = Vector3D.Transform(grav, MatrixD.Transpose(g.WorldMatrix.GetOrientation()));

                //Since the gyro ui lies, we are not trying to control yaw,pitch,roll but rather we
                //need a rotation vector (axis around which to rotate)
                var rot = Vector3D.Cross(localDown, localGrav);
                double ang = rot.Length();
                ang = Math.Atan2(ang, Math.Sqrt(Math.Max(0.0, 1.0 - ang * ang))); //More numerically stable than: ang=Math.Asin(ang)

                if (ang < 0.01)
                {
                    //Close enough

                    if (i == gyros.Count - 1)
                    {
                        //	Echo("Level");
                        lcdtext.Add("Gravity leveled.");
                    }

                    g.SetValueBool("Override", false);
                    continue;
                }
                else if (ang >= 0.01)
                {

                    if (i == gyros.Count - 1)
                    {
                        //	Echo("Off level: " + (ang*180.0/Math.PI).ToString() + " deg");
                        lcdtext.Add("Off level: " + (ang * 180.0 / Math.PI).ToString() + " deg");
                    }


                    //Control speed to be proportional to distance (angle) we have left
                    double ctrl_vel = g.GetMaximum<float>("Yaw") * (ang / Math.PI) * CTRL_COEFF;
                    ctrl_vel = Math.Min(g.GetMaximum<float>("Yaw"), ctrl_vel);
                    ctrl_vel = Math.Max(0.01, ctrl_vel); //Gyros don't work well at very low speeds
                    rot.Normalize();
                    rot *= ctrl_vel;
                    g.SetValueFloat("Pitch", (float)rot.GetDim(0));
                    g.SetValueFloat("Yaw", -(float)rot.GetDim(1));
                    g.SetValueFloat("Roll", -(float)rot.GetDim(2));

                    g.SetValueFloat("Power", 1.0f);
                    g.SetValueBool("Override", true);

                }
            }
        }

        public void FaceTarget()
        {

            //Is facing target = true?
            //bool tface = false;

            //Distance to target.
            Vector3D targetdir = Vector3D.Subtract(targetpos, currentpos);
            double dist = targetdir.Length();
            //Echo("Distance to target: " + dist + " m");
            lcdtext.Add("Target distance: " + dist + " m");

            //Lateral distance.
            Matrix rcor;
            rc.Orientation.GetMatrix(out rcor);
            Vector3D fwd = rcor.Forward;
            Vector3D bwd = rcor.Backward;

            Vector3D fwdworldvec = Vector3D.TransformNormal(bwd, rc.WorldMatrix); //Convert body direction "fwd" to world vector.
            fwdworldvec.Normalize();
            double dotfwd = Vector3D.Dot(targetdir, fwdworldvec);
            Vector3D crossfwd = Vector3D.Cross(targetdir, fwdworldvec);
            //lcdtext.Add("crossfwd:\n\r" + "X: " + crossfwd.X + "\n\rY: " + crossfwd.Y + "\n\rZ: " + crossfwd.Z);

            double targetang = Math.Acos(dotfwd / targetdir.Length());
            Vector3D latdir = Vector3D.Multiply(fwdworldvec, dotfwd);

            double latdist = latdir.Length();
            //Echo("Latdist: " + latdist.ToString() + " m");
            //Echo("Targetang: " + (targetang * (180 / Math.PI)).ToString() + " deg");
            lcdtext.Add("Latdist: " + latdist.ToString() + " m");
            lcdtext.Add("Targetang: " + (targetang * (180 / Math.PI)).ToString() + " deg");

            //Face Target.
            for (int i = 0; i < gyros.Count; ++i)
            {
                var g = gyros[i];

                Matrix or;
                g.Orientation.GetMatrix(out or);


                //For "yaw" rotation to face the target, we need direction vector to target.
                Vector3D gtargetdir = new Vector3D();
                gtargetdir = Vector3D.Subtract(g.WorldMatrix.Translation, targetpos);
                gtargetdir.Normalize();
                Vector3D localTarget = Vector3D.TransformNormal(gtargetdir, MatrixD.Transpose(g.WorldMatrix));

                //Gyro-local orientation vectors Forward.
                Vector3D localFwd = Vector3D.Transform(bwd, MatrixD.Transpose(or));

                //Again, gyro UI lies, so we need rotation axis.
                var rotyaw = Vector3D.Cross(localFwd, localTarget);
                //Echo("rotyaw: " + rotyaw.X.ToString() + ", " + rotyaw.Y.ToString() + ", " + rotyaw.Z.ToString());
                double theta = rotyaw.Length();
                theta = Math.Atan2(theta, Math.Sqrt(Math.Max(0.0, 1.0 - theta * theta))); //More numerically stable than: ang=Math.Asin(theta)
                                                                                          //Echo("thetha: " + theta.ToString());

                //lcdtext.Add("Thetha: " + (theta * (180/Math.PI)).ToString() + " deg");

                if (targetang < 0.01)
                {
                    g.SetValueBool("Override", false);
                    //tface = true;

                    continue;
                }
                else if (targetang >= 0.01)
                {

                    if (i == gyros.Count - 1)
                    {
                        //	Echo("Off target: " + (theta*180.0/Math.PI).ToString() + " deg");
                        lcdtext.Add("Off target: " + (theta * 180.0 / Math.PI).ToString() + " deg");
                    }
                    //tface = false;

                    g.SetValueBool("Override", true);

                    //Control speed to be proportional to distance (angle) we have left.
                    double cvel = g.GetMaximum<float>("Yaw") * (theta / Math.PI) * 0.6;
                    cvel = Math.Min(g.GetMaximum<float>("Yaw"), cvel);
                    cvel = Math.Max(0.01, cvel); //Gyros don't work well at very low speeds
                                                 //double exp = Math.Pow(targetang, 2) / 200;
                                                 //cvel = cvel + Math.Pow(1.01, exp) - 1;
                                                 //lcdtext.Add("Face Ctrl Coeff: " + cvel.ToString());

                    rotyaw.Normalize();
                    rotyaw *= cvel;

                    if (targetang > (120 * (Math.PI / 180)))
                    {
                        //rotyaw.X = rotyaw.X +40;
                        rotyaw.Y = rotyaw.Y + 40;
                        //rotyaw.Z = rotyaw.Z + 40;

                        lcdtext.Add("rotyaw: " + "\n\rX: " + rotyaw.X.ToString() + "\n\rY: " + rotyaw.Y.ToString() + "\n\rZ: " + rotyaw.Z.ToString());

                    }

                    //g.SetValueFloat("Pitch", (float)rotyaw.GetDim(0));
                    g.SetValueFloat("Yaw", -(float)rotyaw.GetDim(1));
                    //g.SetValueFloat("Roll", -(float)rotyaw.GetDim(2));

                    g.SetValueFloat("Power", 1.0f);
                    //		g.SetValueBool("Override", false);

                }

            }

            //return tface;
        }

        public string Travel()
        {

            //Distance to target.
            Vector3D targetdir = Vector3D.Subtract(currentpos, targetpos);
            double dist = targetdir.Length();
            //Echo("Distance to target: " + dist + " m");

            //Lateral distance.
            Matrix rcor;
            rc.Orientation.GetMatrix(out rcor);
            Vector3D fwd = rcor.Forward;

            Vector3D fwdworldvec = Vector3D.TransformNormal(fwd, rc.WorldMatrix); //Convert body direction "fwd" to world vector.
            fwdworldvec.Normalize();
            double dot = Vector3D.Dot(targetdir, fwdworldvec);

            double targetang = Math.Acos(dot / targetdir.Length());
            Vector3D latdir = Vector3D.Multiply(fwdworldvec, dot);

            double latdist = latdir.Length();
            //Echo("Lateral dist: " + latdist.ToString() + " m");
            lcdtext.Add("Lateral dist: " + latdist.ToString() + " m");

            //If target is over 100 m away...
            if (latdist > 100)
            {

                //Set elevation to 40 m.
                SetElevation("Travel");

                //Set speed to 15 m/s.
                SetSpeed(15);

                return "intravel";
            }
            else if (latdist <= 100 && latdist > 20)
            {
                //Set elevation to 40 m.
                SetElevation("Travel");

                //Set speed to 5 m/s.
                SetSpeed(5);

                return "intravel";
            }
            else if (latdist <= 20 && latdist > 5)
            {
                Echo("5 < LATDIST <= 10");
                //Set elevation to target elevation.
                SetElevation("Approach");

                //Set speed to 2 m/s.
                SetSpeed(2);

                return "intravel";
            }
            else if (latdist <= 5 && latdist > 2)
            {
                Echo("5 < LATDIST <= 10");
                //Set elevation to target elevation.
                SetElevation("Approach");

                //Set speed to 2 m/s.
                SetSpeed(0.5);

                return "intravel";
            }
            else
            {

                //Set elevation to target elevation.
                SetElevation("Approach");

                //Set speed to 2 m/s.
                SetSpeed(0);

                poscheck.Add(currentpos - lastpos);

                if (poscheck.Count == 6)
                {
                    double poscheckcalc = 0;

                    for (int i = 0; i < poscheck.Count; i++)
                    {
                        poscheckcalc = poscheckcalc + poscheck[i].Length();
                    }

                    if (Math.Abs(poscheckcalc) <= 1)
                    {


                        for (int i = 0; i < allthrusters.Count; i++)
                        {
                            allthrusters[i].ThrustOverridePercentage = 0.0f;
                        }

                        return "stop";
                    }
                    else
                    {
                        return "intravel";
                    }
                }

                return "intravel";



            }



        }

        public void SetElevation(string order)
        {
            //Get downward raycast info to calculate elevation.
            MyDetectedEntityInfo downray = EnvDetectDown();

            if (downray.HitPosition.HasValue)
            {
                elevation = Vector3D.Distance(downcams[0].GetPosition(), downray.HitPosition.Value);
                Echo("Elevation: " + elevation.ToString() + " m");
                lcdtext.Add("Elevation: " + elevation.ToString() + " m");

            }
            else
            {
                //Echo("Elevation: 320+ m");
                lcdtext.Add("Elevation: 320+ m");
            }

            campos = elevcam.GetPosition();

            //Calculate vertical velocity.
            Vector3D veltot = Vector3D.Subtract(currentpos, lastpos);

            Matrix rcor;
            rc.Orientation.GetMatrix(out rcor);
            Vector3D down = rcor.Down;

            Vector3D downworldvec = Vector3D.TransformNormal(down, rc.WorldMatrix); //Convert body direction "down" to world vector.
            downworldvec.Normalize();

            //Project velocity vector on downward world unit vector.
            double dot = Vector3D.Dot(veltot, downworldvec);
            Vector3D velvert = Vector3D.Multiply(downworldvec, dot);
            //lcdtext.Add("velvert: " + "\n\rX: " + velvert.X.ToString() + "\n\rY: " + velvert.Y.ToString() + "\n\rZ: " + velvert.Z.ToString());

            vertspeed = velvert.Length() * 6 * Math.Sign(velvert.X);
            lcdtext.Add("V vertical: " + vertspeed + " m/s");

            //Calculate elevation difference to target.
            Vector3D targetdir = Vector3D.Subtract(campos, targetpos);

            //Project target direction vector on downward unit vector.
            dot = Vector3D.Dot(targetdir, downworldvec);
            Vector3D elevdiff = Vector3D.Multiply(downworldvec, dot);

            //Transform elevation difference vector to local body vector. Y component = elevation difference.
            Vector3D belevdiff = Vector3D.TransformNormal(elevdiff, MatrixD.Transpose(rc.WorldMatrix));
            //lcdtext.Add("B-elevdiff: " + "\n\rX: " + belevdiff.X.ToString() + "\n\rY: " + belevdiff.Y.ToString() + "\n\rZ: " + belevdiff.Z.ToString());

            double tarediff = belevdiff.Y;
            Echo("Target elev diff: " + tarediff.ToString() + " m");
            lcdtext.Add("Target elev diff: " + tarediff.ToString() + " m");

            double targetelev = 40;

            if (order == "Approach")
            {
                targetelev = elevation - tarediff;
            }
            else if (order == "Travel")
            {
                targetelev = 40;
            }

            double ediff = elevation - targetelev;
            //Echo("ediff: " + ediff.ToString() + " m");
            lcdtext.Add("ediff: " + ediff.ToString() + " m");

            //Control coefficient for thrust, dependent on max thrust, elevation difference, ship mass, acceleration & speed.
            float planetg = (float)rc.GetNaturalGravity().Length();
            //Echo("Gravity: " + planetg.ToString() + " m/s2");  
            lcdtext.Add("Gravity: " + planetg.ToString() + " m/s2");

            //Echo("Total mass: " + mtot.ToString() + " kg" + "\n\rMax Thrust Up: " + maxtup.ToString() + " N");
            lcdtext.Add("Total mass: " + mtot.ToString() + " kg" + "\n\rMax Thrust Up: " + maxtup.ToString() + " N");

            float orfup = (float)(mtot * (planetg + Math.Min(Math.Abs(ediff), 5) + (Math.Pow(3, -vertspeed) / 20))) / maxtup;
            float orfdown = (float)(mtot * (Math.Min((Math.Abs(ediff) / 20), 2) + Math.Pow(3, vertspeed) / 200)) / maxtdown;
            //Echo("Thrust Factor Up: " + orfup.ToString() + "\n\rThrust Factor Down: " + orfdown.ToString());
            lcdtext.Add("Thrust Factor Up: " + orfup.ToString() + "\n\rThrust Factor Down: " + orfdown.ToString());

            if (vertspeed > 20)
            {
                for (int i = 0; i < upthrusters.Count; i++)
                {
                    upthrusters[i].ThrustOverridePercentage = 0.00f;
                }

                for (int i = 0; i < downthrusters.Count; i++)
                {
                    downthrusters[i].ThrustOverridePercentage = 1f;
                }
            }
            else if (vertspeed < -5)
            {
                for (int i = 0; i < upthrusters.Count; i++)
                {
                    upthrusters[i].ThrustOverridePercentage = 1f;
                }

                for (int i = 0; i < downthrusters.Count; i++)
                {
                    downthrusters[i].ThrustOverridePercentage = 0.00f;
                }
            }
            else
            {
                if (elevation >= targetelev + 2)
                {

                    //Echo("Elevation too high...");
                    lcdtext.Add("Elevation too high...");

                    for (int i = 0; i < upthrusters.Count; i++)
                    {
                        upthrusters[i].ThrustOverridePercentage = (float)(mtot * planetg) / maxtup;
                    }

                    for (int i = 0; i < downthrusters.Count; i++)
                    {
                        downthrusters[i].ThrustOverridePercentage = orfdown;
                    }
                }
                else if (elevation <= targetelev - 2)
                {

                    //Echo("Elevation too low...");
                    lcdtext.Add("Elevation too low...");

                    for (int i = 0; i < upthrusters.Count; i++)
                    {
                        upthrusters[i].ThrustOverridePercentage = orfup;
                    }

                    for (int i = 0; i < downthrusters.Count; i++)
                    {
                        downthrusters[i].ThrustOverridePercentage = 0.0f;
                    }
                }
                else if (elevation < targetelev + 2 && elevation > targetelev + 0.4)
                {
                    if (vertspeed > 0)
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = (float)(mtot * (planetg)) / maxtup;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.05f;
                        }
                    }
                    else if (vertspeed < -0.5)
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = (float)(mtot * (planetg + 2)) / maxtup;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.00f;
                        }
                    }
                }
                else if (elevation > targetelev - 2 && elevation < targetelev - 0.1)
                {
                    if (vertspeed > 0.4)
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = (float)(mtot * (planetg)) / maxtup;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.01f;
                        }
                    }
                    else if (vertspeed < 0)
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = (float)(mtot * (planetg + 2)) / maxtup;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.00f;
                        }
                    }
                }
                else
                {
                    //Echo("Elevation OK...");
                    lcdtext.Add("Elevation OK...");

                    if (vertspeed > 0.2)
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = (float)(mtot * (planetg)) / maxtup;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.01f;
                        }
                    }
                    else if (vertspeed < -0.1)
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = (float)(mtot * (planetg + 1)) / maxtup;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.00f;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < upthrusters.Count; i++)
                        {
                            upthrusters[i].ThrustOverridePercentage = 0.0f;
                        }

                        for (int i = 0; i < downthrusters.Count; i++)
                        {
                            downthrusters[i].ThrustOverridePercentage = 0.0f;
                        }
                    }
                }
            }
        }

        public void SetSpeed(double targetspeed)
        {

            //Calculate lateral velocity.
            Vector3D veltot = Vector3D.Subtract(currentpos, lastpos);

            Matrix rcor;
            rc.Orientation.GetMatrix(out rcor);
            Vector3D fwd = rcor.Forward;

            Vector3D fwdworldvec = Vector3D.TransformNormal(fwd, rc.WorldMatrix); //Convert body direction "fwd" to world vector.
            fwdworldvec.Normalize();

            //Project velocity vector on forwrd world unit vector.
            double dot = Vector3D.Dot(veltot, fwdworldvec);
            Vector3D vellat = Vector3D.Multiply(fwdworldvec, dot);
            //lcdtext.Add("vellat: " + "\n\rX: " + vellat.X.ToString() + "\n\rY: " + vellat.Y.ToString() + "\n\rZ: " + vellat.Z.ToString());

            latspeed = vellat.Length() * 6 * Math.Sign(vellat.Z);
            lcdtext.Add("V lateral: " + latspeed + " m/s");

            if (targetspeed <= 5)
            {
                if (latspeed > targetspeed)
                {
                    for (int i = 0; i < fwdthrusters.Count; i++)
                    {
                        fwdthrusters[i].ThrustOverridePercentage = 0.0f;
                    }

                    for (int i = 0; i < bwdthrusters.Count; i++)
                    {
                        bwdthrusters[i].ThrustOverridePercentage = 0.1f;
                    }
                }
                else if (latspeed < targetspeed)
                {
                    for (int i = 0; i < fwdthrusters.Count; i++)
                    {
                        fwdthrusters[i].ThrustOverridePercentage = 0.1f;
                    }

                    for (int i = 0; i < bwdthrusters.Count; i++)
                    {
                        bwdthrusters[i].ThrustOverridePercentage = 0.0f;
                    }
                }
            }
            else
            {
                if (latspeed > targetspeed)
                {
                    for (int i = 0; i < fwdthrusters.Count; i++)
                    {
                        fwdthrusters[i].ThrustOverridePercentage = 0.0f;
                    }

                    for (int i = 0; i < bwdthrusters.Count; i++)
                    {
                        bwdthrusters[i].ThrustOverridePercentage = 1f;
                    }
                }
                else if (latspeed < targetspeed)
                {
                    for (int i = 0; i < fwdthrusters.Count; i++)
                    {
                        fwdthrusters[i].ThrustOverridePercentage = 0.6f;
                    }

                    for (int i = 0; i < bwdthrusters.Count; i++)
                    {
                        bwdthrusters[i].ThrustOverridePercentage = 0.0f;
                    }
                }
            }

        }

        public MyDetectedEntityInfo EnvDetectDown()
        {
            MyDetectedEntityInfo info = new MyDetectedEntityInfo();
            double downrange = 320.0;

            if (firstraycast)
            {
                firstraycast = false;
                elevcam.EnableRaycast = true;
            }

            //Raycast with down-pointing camera.
            if (elevcam.CanScan(downrange))
            {
                info = elevcam.Raycast(downrange, 0, 0);
            }


            return info;

        }

        public void LCDPrint(List<string> lcdtext)
        {
            string output = lcdtext[0];

            for (int i = 1; i < lcdtext.Count; i++)
            {
                output = output + "\n\r" + lcdtext[i];
            }

            lcd.WritePublicText(output, false);
            lcd.ShowPublicTextOnScreen();
        }

        public void ParseTransmission(string transmission)
        {
            if (transmission.StartsWith("DMST"))
            {
                string[] message = transmission.Split('_');
                string name = message[1];
                List<string> tdata = new List<string>();

                if (name == droneid)
                {
                    for (int i = 2; i < message.Length; i++)
                    {
                        tdata.Add(message[i]);
                    }

                    ReadOrders(tdata);
                }
            }
            else
            {
                Echo("Unknown transmission received.");
            }
        }

        public void ReadOrders(List<string> tdata)
        {
            string order = tdata[0];

            if (order == "NewName")
            {
                droneid = tdata[1];
                rc.CustomName = droneid + " RC";
                antenna.CustomName = droneid;
                idchange = true;
            }
        }

        public void UpdateMother()
        {
            Echo("Sending update to Mother...");
            Echo(travelorder);

            string stringpos = ":" + currentpos.X.ToString() + ":" + currentpos.Y.ToString() + ":" + currentpos.Z.ToString();

            string status = travelorder;
            if (status == "intravel")
            {
                status = "Travelling";
            }
            else if (status == "stop")
            {
                status = "Idle";
            }

            if (idchange)
            {

                antenna.TransmitMessage("DMST_" + droneid + "_" + status + "_NameChange_" + stringpos, MyTransmitTarget.Default);
                Echo("Name change confirmation sent.");
                idchange = false;
            }


            string message = "DMST_" + droneid + "_" + status + "_" + mission + "_" + stringpos;
            antenna.TransmitMessage(message, MyTransmitTarget.Default);
            Echo("Transmission to Mother: \n\r" + message);

        }
    }
}