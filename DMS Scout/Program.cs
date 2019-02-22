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

        IMyTextPanel statuslcd;
        IMyTextPanel menulcd;
        IMyRadioAntenna antenna;
        IMyCockpit cockpit;
        IMyInteriorLight indicator;

        bool setupcomplete = false;
        bool motherinrange = false;

        List<string> deposits = new List<string>();

        int menupos = 0;

        string[] menu = new string[10] {
"Iron",
"Nickel",
"Magnesium",
"Silicon",
"Cobalt",
"Silver",
"Gold",
"Platinum",
"Uranium",
"Ice" };

        int tick = 0;


        public void Main(string arg, UpdateType updatesource)

        {

            //Run Setup if it hasn't been run, or if no antenna is assigned.
            if (!setupcomplete | antenna == null)
            {
                Setup();
            }
            else
            {

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
                        DisplayMenu(arg);
                    }
                    //??? ELSE IF triggered with default argument in PB terminal, or other unknown terminal block???
                }

                //If script is triggered by antenna.
                if ((updatesource & UpdateType.Antenna) != 0)
                {
                    Echo("Script triggered by antenna.");

                    //Mining Scout transmission. Message delimited by underscore ('_') and structured as follows.
                    //MSDMST_OreType_Position_RecommendedApproachDirection_"Confirmed"
                    if (arg.StartsWith("MSDMST"))
                    {


                        string[] message = arg.Split('_');
                        if (message[1] == "Ping")
                        {
                            if (!motherinrange)
                            {
                                SendUnconfirmed();
                                indicator.Color = Color.Green;
                            }

                            motherinrange = true;

                        }
                        else
                        {
                            if (message[4] == "Confirmed")
                            {

                                for (int i = 0; i < deposits.Count; i++)
                                {
                                    string[] split = deposits[i].Split('_');
                                    if (split[1] == message[2])
                                    {
                                        Echo(split[0] + "at " + split[1] + "\n\r" + "confirmed by Mother.");
                                        deposits[i] = "x" + deposits[i];

                                        DisplayStatus(i);
                                    }
                                }
                            }
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

                    if (tick == 5)
                    {
                        string message = "MSDMST_Ping";
                        antenna.TransmitMessage(message, MyTransmitTarget.Default);

                        if (!motherinrange)
                        {
                            indicator.Color = Color.LightYellow;
                        }

                        motherinrange = false;
                        tick = 0;
                    }

                    DisplayMenu(" ");
                    DisplayStatus(-1);
                    tick++;
                }
            }
        }

        public void DisplayMenu(string arg)
        {

            menulcd.WritePublicText("SELECT DEPOSIT TO ADD: \n\r", false);

            if (arg == "menuselect")
            {
                string ore = menu[menupos];

                Vector3D currentpos = cockpit.GetPosition();

                if (deposits.Count > 0)
                {
                    for (int i = 0; i < deposits.Count; i++)
                    {
                        string[] split = deposits[i].Split('_');
                        if (split[0].Contains(ore))
                        {
                            Vector3D distvec = StringToVector3D(":" + split[1]);
                            double dist = distvec.Length();

                            if (dist < 50)
                            {
                                menulcd.WritePublicText("ERROR: \n\r" + ore + " at this location already added!", false);
                            }
                            else
                            {
                                Matrix cor;
                                cockpit.Orientation.GetMatrix(out cor);
                                Vector3D fwd = cor.Forward;
                                Vector3D fwdworldvec = Vector3D.TransformNormal(fwd, cockpit.WorldMatrix); //Convert body direction "fwd" to world vector.
                                fwdworldvec.Normalize();

                                string newdeposit = ore + "_" + currentpos + "_" + fwdworldvec;
                                deposits.Add(newdeposit);

                                menulcd.WritePublicText("Adding " + ore + " location.", false);
                                string message = "MSDMST_" + newdeposit;
                                antenna.TransmitMessage(message, MyTransmitTarget.Default);
                            }
                        }
                    }
                }
                else
                {
                    Matrix cor;
                    cockpit.Orientation.GetMatrix(out cor);
                    Vector3D fwd = cor.Forward;
                    Vector3D fwdworldvec = Vector3D.TransformNormal(fwd, cockpit.WorldMatrix); //Convert body direction "fwd" to world vector.
                    fwdworldvec.Normalize();

                    string newdeposit = ore + "_" + currentpos + "_" + fwdworldvec;
                    deposits.Add(newdeposit);

                    menulcd.WritePublicText("Adding " + ore + " location.", false);
                    string message = "MSDMST_" + newdeposit;
                    antenna.TransmitMessage(message, MyTransmitTarget.Default);
                }
            }
            else
            {
                if (arg == "menuup")
                {
                    if (menupos == 0)
                    {
                        menupos = menu.Length - 1;
                    }
                    else
                    {
                        menupos--;
                    }
                }
                else if (arg == "menudown")
                {
                    if (menupos == menu.Length - 1)
                    {
                        menupos = 0;
                    }
                    else
                    {
                        menupos++;
                    }
                }

                for (int i = 0; i < menu.Length; i++)
                {
                    if (i == menupos)
                    {
                        menulcd.WritePublicText("   " + menu[i] + " <-- \n\r", true);
                    }
                    else
                    {
                        menulcd.WritePublicText("   " + menu[i] + "\n\r", true);
                    }
                }
            }
        }

        public void DisplayStatus(int arg)
        {
            statuslcd.WritePublicText("  ---STATUS---  \n\r", false);

            if (!motherinrange & tick == 5)
            {
                statuslcd.WritePublicText("MOTHER OOR \n\r", true);
            }
            else
            {
                statuslcd.WritePublicText("Mother is in range. \n\r", true);
            }

            if (arg > -1)
            {
                string[] split = deposits[arg].Split('_');

                statuslcd.WritePublicText("\n\rMother confirmed: \n\r" + split[0] + " at " + split[1], true);
            }
            else
            {
                statuslcd.WritePublicText("\n\rRecently confirmed: \n\r", true);

                int added = 0;

                for (int i = deposits.Count - 1; added <= 3 & i >= 0; i--)
                {
                    if (deposits[i].StartsWith("x"))
                    {
                        added++;
                        string[] split = deposits[i].Split('_');
                        string ore = split[0].Substring(1);
                        statuslcd.WritePublicText(ore + "\n\r", true);
                    }
                }
            }
        }

        public void SendUnconfirmed()
        {
            bool sent = false;

            for (int i = 0; i < deposits.Count & !sent; i++)
            {
                if (!deposits[i].StartsWith("x"))
                {
                    string message = "MSDMST_" + deposits[i];
                    antenna.TransmitMessage(message, MyTransmitTarget.Default);
                    sent = true;
                }
            }
        }

        public void Setup()
        {
            var l = new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlocksOfType(l, x => Me.IsSameConstructAs(x));

            foreach (var block in l)
            {
                if (block is IMyRadioAntenna)
                {
                    antenna = block as IMyRadioAntenna;
                }
                else if (block is IMyCockpit)
                {
                    cockpit = block as IMyCockpit;
                }
                else if (block is IMyTextPanel & block.CustomName == "Menu LCD")
                {
                    menulcd = block as IMyTextPanel;
                    menulcd.ShowPublicTextOnScreen();
                }
                else if (block is IMyTextPanel & block.CustomName == "Status LCD")
                {
                    statuslcd = block as IMyTextPanel;
                    statuslcd.ShowPublicTextOnScreen();
                }
                else if (block is IMyInteriorLight & block.CustomName == "Indicator")
                {
                    indicator = block as IMyInteriorLight;
                }

                if (antenna != null & cockpit != null & menulcd != null & statuslcd != null & indicator != null)
                {
                    setupcomplete = true;
                    Echo("Setup complete.");

                    string message = "MSDMST_Ping";
                    antenna.TransmitMessage(message, MyTransmitTarget.Default);
                }
                else
                {
                    Echo("ERROR: Setup incomplete.");
                }

            }
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