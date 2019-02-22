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


            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            //This makes the program automatically run every 100 ticks.



        }

        List<IMyShipConnector> upconnectors = new List<IMyShipConnector>();
        List<IMyShipConnector> downconnectors = new List<IMyShipConnector>();

        List<IMyTextPanel> guidelcds = new List<IMyTextPanel>();
        IMyTextPanel statuslcd;
        IMyTextPanel menulcd;
        IMyTextPanel mininglcd;
        IMyTextPanel alertslcd;

        IMyRadioAntenna antenna;

        bool setupcomplete = false;
        bool droneidcomplete = false;
        bool nodrones = false;
        bool nodeposits = false;

        List<string> drones = new List<string>();
        List<string> droneticks = new List<string>();
        List<string> idregistry = new List<string>();

        int menupos = 0;
        int menunr = 0;
        int menulength;
        int ordernr = 0;

        string[] startmenu = new string[3] {
        "Select drone...",
        "Auto-assign: ",
        "Recall all drones" };

        string[] missionmenu = new string[3] {
        "Mining...",
        "Recall",
        "Unregister" };

        string[] miningmenu = new string[11] {
        "Iron",
        "Nickel",
        "Magnesium",
        "Silicon",
        "Cobalt",
        "Silver",
        "Gold",
        "Platinum",
        "Uranium",
        "Ice",
        "Stone" };

        int[] displayinfo;
        bool autoassign = false;
        int selecteddrone = -1;
        int selectedore = -1;

        List<string> deposits = new List<string>();
        List<string> dronestatus = new List<string>();

        Vector3D currentpos = new Vector3D();

        bool havedrones = false;

        int tick = 0;


        public void Main(string arg, UpdateType updatesource)

        {
            
            //Set up drone ID list on first run.
            if (!droneidcomplete)
            {
                DroneIDSetup();
            }

            //Run Setup if it hasn't been run, or if no antenna is assigned.
            if (!setupcomplete | antenna == null)
            {
                Setup();
            }
            else
            {

                for (int i = 0; i < idregistry.Count; i++)
                {
                    Echo(idregistry[i]);
                }

                //If script is triggered by terminal or trigger (button, sensor, etc).
                if ((updatesource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
                {
                    Echo("Script triggered by terminal block.");

                    if (arg == "setup")
                    {
                        Setup();
                    }
                    else if (arg.StartsWith("menu"))
                    {
                        displayinfo = UpdateMenuDisplay(arg);
                        MissionAssign("Menu");
                        DisplayMenu();
                        ordernr = 0;
                    }
                    //??? ELSE IF triggered with default argument in PB terminal, or other unknown terminal block???
                }

                //If script is triggered by antenna.
                if ((updatesource & UpdateType.Antenna) != 0)
                {
                    Echo("Script triggered by antenna.");

                    //If message is correctly formatted... else echo a warning.
                    if (arg.StartsWith("DMST"))
                    {
                        string[] message = arg.Split('_');

                        //Analyse received message.
                        //Messages are delimited by underscores ('_') and structured as follows:
                        //DMST_drone name_status_mission_position
                        string name = message[1];
                        string status = message[2];
                        string mission = message[3];
                        Vector3D position = StringToVector3D(message[4]);

                        //If drone is new (name by default contains "xxx"), assign new ID number, and send to drone.
                        if (name.Contains("xxx"))
                        {
                            string newid;
                            bool changed = false;

                            for (int i = 0; i < idregistry.Count & !changed; i++)
                            {
                                if (!idregistry[i].StartsWith("t"))
                                {
                                    newid = idregistry[i];
                                    changed = true;
                                    string newname = name.Replace("xxx", newid);

                                    antenna.TransmitMessage(("DMST_" + name + "_NewName_" + newname), MyTransmitTarget.Default);
                                    Echo("Sent new ID " + newname + " to drone " + name + ".");
                                }

                            }


                        }
                        else if (!name.Contains("xxx"))
                        {
                            currentpos = antenna.GetPosition();
                            Vector3D distancevector = (position - currentpos);
                            double distance = distancevector.Length();
                            distance = Convert.ToInt32(distance);
                            bool newdrone = true;

                            string droneupdate = name + "_" + status + "_" + mission + "_" + distance.ToString() + "m";

                            for (int i = 0; i < dronestatus.Count & newdrone; i++)
                            {
                                if (dronestatus[i].StartsWith(name))
                                {
                                    dronestatus[i] = droneupdate;
                                    newdrone = false;
                                }
                            }
                            if (newdrone)
                            {
                                dronestatus.Add(droneupdate);
                                Echo("New drone detected");
                                bool found = false;

                                for (int i = 0; i < idregistry.Count & !found; i++)
                                {
                                    if (name.EndsWith(idregistry[i])) ;
                                    {
                                        found = true;

                                        if (!(idregistry[i].StartsWith("t")))
                                        {
                                            drones.Add(name);
                                            droneticks.Add(name + "_0");
                                            idregistry[i] = "t" + idregistry[i];
                                        }
                                        else if (idregistry[i].StartsWith("t"))
                                        {
                                            Echo("Duplicate drone ID found: " + name);
                                        }
                                    }
                                }
                            }

                            bool isinlist = false;
                            for (int i = 0; i < droneticks.Count; i++)
                            {
                                string[] split = droneticks[i].Split('_');

                                if (split[0] == drones[i])
                                {
                                    int dtick = Convert.ToInt32(split[1]);
                                    isinlist = true;

                                    if (dtick < 5)
                                    {
                                        dtick++;
                                        string dsplice = split[0] + "_" + dtick.ToString();
                                        droneticks[i] = dsplice;
                                    }
                                }

                                if (!isinlist)
                                {
                                    droneticks.Add(split[0] + "_0");
                                }
                            }
                        }

                        //If drone name change was successful, confirmation message is sent from drone.
                        //Mark ID as unavailable in registry and add new drone name to list of active drones.
                        if (mission == "NameChange")
                        {
                            drones.Add(name);
                            droneticks.Add(name + "_0");

                            string[] split = name.Split('-');
                            string newid = split[1];

                            bool check = false;
                            for (int i = 0; i < idregistry.Count & !check; i++)
                            {
                                if (idregistry[i] == newid)
                                {
                                    idregistry[i] = "t" + idregistry[i];
                                    check = true;

                                    Echo("Drone " + name + " registered.");
                                }

                            }
                        }
                    }
                    //Mining Scout transmission. Message delimited by underscore ('_') and structured as follows.
                    //MSDMST_OreType_Position_RecommendedApproachDirection
                    else if (arg.StartsWith("MSDMST"))
                    {
                        string[] message = arg.Split('_');

                        if (message[1] == "Ping")
                        {
                            antenna.TransmitMessage(arg, MyTransmitTarget.Default);
                        }
                        else
                        {
                            int idx = deposits.Count;
                            string newdeposit = message[1] + "_" + message[2] + "_" + message[3] + "_" + idx.ToString();

                            deposits.Add(newdeposit);

                            string confmessage = arg + "_Confirmed";
                            antenna.TransmitMessage(confmessage, MyTransmitTarget.Default);
                        }
                    }
                    else
                    {
                        Echo("Invalid transmission received:\n" + arg);
                    }
                }

                //If script is triggered by runtime frequency.
                if ((updatesource & UpdateType.Update100) != 0)
                {
                    Echo("Script triggered by runtime.");


                    tick++;

                    if (drones.Count > 0)
                    {
                        havedrones = true;

                        if (autoassign)
                        {
                            MissionAssign("Auto");
                        }


                    }
                    else
                    {
                        havedrones = false;
                    }

                    if (tick == 5)
                    {
                        DisplayStatus();
                        DisplayMining();


                        for (int i = 0; i < droneticks.Count; i++)
                        {
                            string[] split = droneticks[i].Split('_');
                            int dtick = Convert.ToInt32(split[1]);

                            if (dtick > 0)
                            {
                                dtick--;
                                string dsplice = split[0] + "_" + dtick;
                                droneticks[i] = dsplice;
                            }
                            else
                            {
                                for (int k = 0; k < dronestatus.Count; k++)
                                {
                                    if (dronestatus[k].StartsWith(split[0]))
                                    {
                                        dronestatus[k] = split[0] + "  -  CONNECTION LOST";
                                        DisplayAlerts("Connection lost with drone " + split[0]);
                                    }
                                }
                            }
                        }

                        tick = 0;
                    }



                }

                if (droneticks.Count > 0)
                {
                    Echo(droneticks[0]);
                }
            }

        }


        public int[] UpdateMenuDisplay(string arg)
        {
            if (arg == "menuUp" | arg == "menuDown")
            {

                ordernr = 0;

                if (menunr == 0)
                {
                    menulength = startmenu.Length;
                }
                else if (menunr == 1)
                {
                    menulength = drones.Count;
                }
                else if (menunr == 2)
                {
                    menulength = missionmenu.Length;
                }
                else if (menunr == 3)
                {
                    menulength = miningmenu.Length;
                }


                if (arg == "menuUp")
                {
                    if (menupos > 0)
                    {
                        menupos = menupos - 1;
                    }
                    else
                    {
                        menupos = menulength - 1;
                    }
                }
                else if (arg == "menuDown")
                {
                    if (menupos < menulength - 1)
                    {
                        menupos = menupos + 1;
                    }
                    else if (menupos == menulength - 1)
                    {
                        menupos = 0;
                    }

                }
            }

            //ordernr: 0 = default, do nothing, 1 = recall all drones, 
            //ordernr: 2 = recall drone, 3 = assign mining mission to drone.
            if (arg == "menuSelect")
            {
                if (menunr == 0)
                {
                    if (menupos == 0)
                    {
                        menunr = 1;
                        menupos = 0;
                        ordernr = 0;
                    }
                    else if (menupos == 1 & !autoassign)
                    {
                        autoassign = true;
                    }
                    else if (menupos == 1 & autoassign)
                    {
                        autoassign = false;
                    }
                    else if (menupos == 2)
                    {
                        ordernr = 1;
                    }
                }
                else if (menunr == 1 & !nodrones)
                {
                    selecteddrone = menupos;
                    menunr = 2;
                }
                else if (menunr == 2)
                {
                    if (menupos == 0)
                    {
                        menupos = 0;
                        menunr = 3;
                    }
                    else if (menupos == 1)
                    {
                        ordernr = 2;
                    }
                }
                else if (menunr == 3 & !nodeposits)
                {
                    selectedore = menupos;
                    ordernr = 3;
                    menunr = 0;
                    menupos = 0;
                }

            }

            if (arg == "menuBack")
            {
                menunr = menunr - 1;

                if (menunr == 1)
                {
                    selecteddrone = -1;
                }
                else if (menunr == 2)
                {
                    menupos = selecteddrone;
                }

            }

            displayinfo = new int[3] {
    menunr,
    menupos,
    ordernr };
            return displayinfo;

        }

        public void DisplayMenu()
        {

            List<string> currentmenu = new List<string>();

            menulcd.WritePublicText("           -=DMS Menu=-         \n\r", false);

            if (menunr == 0)
            {
                for (int i = 0; i < startmenu.Length; i++)
                {
                    currentmenu.Add(startmenu[i]);
                }
            }
            else if (menunr == 1 & drones.Count > 0)
            {
                for (int i = 0; i < drones.Count; i++)
                {
                    currentmenu.Add(drones[i]);
                }
            }
            else if (menunr == 1 & drones.Count == 0)
            {
                nodrones = true;
                DisplayAlerts("NO DRONES ACTIVE!");
            }
            else if (menunr == 2)
            {
                for (int i = 0; i < missionmenu.Length; i++)
                {
                    currentmenu.Add(missionmenu[i]);
                }
            }
            else if (menunr == 3 & deposits.Count > 0)
            {
                for (int i = 0; i < miningmenu.Length; i++)
                {
                    currentmenu.Add(miningmenu[i]);
                }
            }
            else if (menunr == 3 & deposits.Count == 0)
            {
                nodeposits = true;
                DisplayAlerts("No deposits scouted.");
            }

            if ((!nodrones & !nodeposits) & menunr > 0 & currentmenu.Count > 0)
            {
                for (int i = 0; i < currentmenu.Count; i++)
                {
                    if (i == menupos)
                    {
                        menulcd.WritePublicText((currentmenu[i] + " <--\n\r"), true);
                    }
                    else
                    {
                        menulcd.WritePublicText(currentmenu[i] + "\n\r", true);
                    }
                }
            }
            else if (menunr == 0)
            {
                for (int i = 0; i < currentmenu.Count; i++)
                {
                    if (i == menupos & i == 1 & autoassign)
                    {
                        menulcd.WritePublicText((currentmenu[i] + " ON <--\n\r"), true);
                    }
                    else if (i == menupos & i == 1 & !autoassign)
                    {
                        menulcd.WritePublicText((currentmenu[i] + " OFF <--\n\r"), true);
                    }
                    else if (i == menupos)
                    {
                        menulcd.WritePublicText((currentmenu[i] + " <--\n\r"), true);
                    }
                    else
                    {
                        if (i != 1)
                        {
                            menulcd.WritePublicText(currentmenu[i] + "\n\r", true);
                        }
                        else
                        {
                            if (autoassign)
                            {
                                menulcd.WritePublicText(currentmenu[i] + " ON\n\r", true);
                            }
                            else
                            {
                                menulcd.WritePublicText(currentmenu[i] + " OFF\n\r", true);
                            }
                        }
                    }
                }
            }
            else if (menunr == 1 & nodrones)
            {
                menulcd.WritePublicText("ERROR: No active drones.\n\r", true);
            }
            else if (menunr == 3 & nodeposits)
            {
                menulcd.WritePublicText("ERROR: No deposits scouted.\n\r", true);
            }

            menulcd.ShowPublicTextOnScreen();
        }

        public void DisplayStatus()
        {
            statuslcd.WritePublicText("DRONE ID  -  STATUS  -  MISSION  -  DISTANCE\n\r", false);

            for (int i = 0; i < dronestatus.Count; i++)
            {
                statuslcd.WritePublicText(dronestatus[i] + "\n\r", true);
            }

            statuslcd.ShowPublicTextOnScreen();
        }

        public void DisplayMining()
        {
            mininglcd.WritePublicText("TYPE  -  POS  -  AV\n\r", false);

            for (int i = 0; i < deposits.Count; i++)
            {
                if (deposits[i].EndsWith("/") | deposits[i].EndsWith("\\"))
                {
                    deposits[i].Replace("/", "|");
                    deposits[i].Replace("\\", "|");
                }
                else if (deposits[i].EndsWith("|"))
                {
                    deposits[i].Replace("|", "/");
                }

                mininglcd.WritePublicText(deposits[i] + "\n\r", true);
            }

            mininglcd.ShowPublicTextOnScreen();
        }

        public void DisplayAlerts(string arg)
        {
            if (arg == "firstrun")
            {
                alertslcd.WritePublicText("DMS Alerts: " + "\n\r", false);
                alertslcd.ShowPublicTextOnScreen();
            }
            else
            {
                alertslcd.WritePublicText(arg + "\n\r", true);
                alertslcd.ShowPublicTextOnScreen();
            }
        }

        //Mission assignment method.
        //ordernr: 0 = default, do nothing, 1 = recall all drones, 
        //ordernr: 2 = recall drone, 3 = assign mining mission to drone.
        public void MissionAssign(string ordersource)
        {
            if (ordersource == "Menu")
            {
                if (ordernr == 1 & havedrones)
                {
                    currentpos = antenna.GetPosition();

                    antenna.TransmitMessage("DMST_AllDrones_Recall_:" + currentpos.X.ToString() + ":" + currentpos.Y.ToString() + ":" + currentpos.Z.ToString(), MyTransmitTarget.Default);
                }
                else if (ordernr == 2 & havedrones)
                {
                    currentpos = antenna.GetPosition();

                    antenna.TransmitMessage("DMST_" + drones[selecteddrone] + "_Recall_:" + currentpos.X.ToString() + ":" + currentpos.Y.ToString() + ":" + currentpos.Z.ToString(), MyTransmitTarget.Default);
                }
                else if (ordernr == 3 & havedrones)
                {
                    currentpos = antenna.GetPosition();

                    var depositcomp = new List<string>();
                    var distances = new List<double>();

                    for (int i = 0; i < deposits.Count; i++)
                    {
                        if (deposits[i].StartsWith(miningmenu[selectedore]))
                        {
                            string[] split = deposits[i].Split('_');
                            Vector3D depositpos = StringToVector3D(split[1]);
                            double distance = (depositpos - currentpos).Length();

                            depositcomp.Add(distance.ToString() + " " + i.ToString());
                            distances.Add(distance);
                        }
                    }

                    if (distances.Count > 0)
                    {
                        double min = distances.Min();
                        int idx;

                        for (int k = 0; k < depositcomp.Count; k++)
                        {
                            if (depositcomp[k].StartsWith(min.ToString()))
                            {
                                string[] split = depositcomp[k].Split(' ');
                                idx = Convert.ToInt32(split[1]);
                                k = depositcomp.Count;

                                split = deposits[idx].Split(' ');
                                deposits[idx] = deposits[idx] + "  -  /";
                                string splice = split[0] + "_" + split[1] + "_" + split[2];

                                antenna.TransmitMessage("DMST_" + drones[selecteddrone] + "_Mining_" + splice, MyTransmitTarget.Default);
                                selecteddrone = -1;
                                selectedore = -1;
                            }
                        }

                    }
                    else
                    {
                        Echo("Mining mission for drone " + drones[selecteddrone] + " aborted: no deposits scouted.");
                        DisplayAlerts("No mining deposits scouted!");
                    }
                }

                ordernr = 0;
            }
            else if (ordersource == "Auto")
            {
                for (int i = 0; i < dronestatus.Count; i++)
                {
                    string[] split = dronestatus[i].Split('_');
                    string droneid = split[0];
                    string dstatus = split[1];
                    string dmission = split[2];

                    if (dstatus == "Idle" & dmission == "None")
                    {
                        currentpos = antenna.GetPosition();

                        var depositcomp = new List<string>();
                        var distances = new List<double>();

                        for (int j = 0; j < deposits.Count; j++)
                        {
                            if (!(deposits[j].EndsWith("/") | deposits[j].EndsWith("|") | deposits[j].EndsWith("\\")))
                            {
                                string[] tsplit = deposits[j].Split('_');
                                Vector3D depositpos = StringToVector3D(tsplit[1]);
                                double distance = (depositpos - currentpos).Length();

                                depositcomp.Add(distance.ToString() + " " + j.ToString());
                                distances.Add(distance);
                            }
                        }

                        if (distances.Count > 0)
                        {
                            double min = distances.Min();
                            int idx;

                            for (int k = 0; k < depositcomp.Count; k++)
                            {
                                if (depositcomp[k].StartsWith(min.ToString()))
                                {
                                    split = depositcomp[k].Split(' ');
                                    idx = Convert.ToInt32(split[1]);
                                    k = depositcomp.Count;

                                    split = deposits[idx].Split(' ');
                                    deposits[idx] = deposits[idx] + "  -  /";
                                    string splice = split[0] + "_" + split[1] + "_" + split[2];

                                    antenna.TransmitMessage("DMST_" + droneid + "_Mining_" + splice, MyTransmitTarget.Default);
                                }
                            }

                        }
                        else
                        {
                            Echo("Mining mission for drone " + droneid + " aborted: no deposits scouted.");
                            DisplayAlerts("No mining deposits scouted!");
                        }
                    }
                }
            }
        }

        public void Setup()
        {
            Echo("Running Setup...");

            var l = new List<IMyShipConnector>();
            string bname;

            //Get DMS connectors.
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(l, x => x.CubeGrid == Me.CubeGrid);

            if (l.Count > 0)
            {
                for (int i = 0; i < l.Count; i++)
                {
                    bname = l[i].CustomName;

                    if (bname.Contains("[DMS Up]"))
                    {
                        upconnectors.Add(l[i]);
                    }
                    else if (bname.Contains("[DMS Down]"))
                    {
                        downconnectors.Add(l[i]);
                    }
                }
            }
            else
            {
                Echo("No DMS-tagged connector found.");
            }

            //Get DMS LCDs.
            var m = new List<IMyTextPanel>();

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(m, x => x.CubeGrid == Me.CubeGrid);

            if (m.Count() > 0)
            {
                for (int i = 0; i < m.Count; i++)
                {
                    bname = m[i].CustomName;

                    if (bname.Contains("[DMS Status]"))
                    {
                        statuslcd = m[i];
                    }
                    else if (bname.Contains("[DMS Menu]"))
                    {
                        menulcd = m[i];
                    }
                    else if (bname.Contains("[DMS Mining]"))
                    {
                        mininglcd = m[i];
                    }
                    else if (bname.Contains("[DMS Alerts]"))
                    {
                        alertslcd = m[i];
                        DisplayAlerts("firstrun");
                    }
                    else if (bname.Contains("[DMS Guide"))
                    {
                        guidelcds.Add(m[i]);
                    }
                }

            }
            else
            {
                Echo("No DMS-tagged LCDs found.");
            }

            //Get antenna block.
            var n = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(n, x => x.CubeGrid == Me.CubeGrid);

            if (n.Count > 0)
            {
                antenna = n[0];
                setupcomplete = true;
                Echo("Setup complete.");

            }
            else
            {
                Echo("No antenna found. Setup incomplete.");
            }
        }

        public void DroneIDSetup()
        {
            Echo("Running Droid ID setup...");

            for (int i = 1; i <= 99; i++)
            {
                idregistry.Add(i.ToString());

                for (int k = idregistry[i - 1].Length; k <= 2; k++)
                {
                    idregistry[i - 1] = "0" + idregistry[i - 1];
                }
            }

            droneidcomplete = true;
            Echo("Drone ID setup complete.");


        }

        public Vector3D StringToVector3D(string stringpos)
        {
            var coords = new List<float>();
            var position = new Vector3D();

            //String pattern for parsing Vector3D coordinates.
            string pattern = @":(-*\d*\.*\d*):(-*\d*\.*\d*):(-*\d*\.*\d*)";
            System.Text.RegularExpressions.Regex rgx = new System.Text.RegularExpressions.Regex(pattern);

            System.Text.RegularExpressions.MatchCollection matches = rgx.Matches(stringpos);
            System.Text.RegularExpressions.Match match = null;

            if (matches.Count > 0)
            {
                match = matches[0];

                for (int i = 0; i < match.Groups.Count; i++)
                {
                    float coord;
                    if (float.TryParse(match.Groups[i].ToString(), out coord))
                    {
                        coords.Add(coord);
                    }
                }
            }

            position.X = coords[0];
            position.Y = coords[1];
            position.Z = coords[2];
            return position;
        }

    }
}