﻿/*
 * Copyright © 2015 - 2022 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 */

using EliteDangerousCore;
using EliteDangerousCore.DB;
using QuickJSON;
using System;
using System.Linq;

namespace EDDiscovery
{
    public partial class EDDiscoveryForm
    {
        private EDDDLLInterfaces.EDDDLLIF.EDDCallBacks DLLCallBacks;

        public void DLLStart()
        {
            EliteDangerousCore.DLL.EDDDLLAssemblyFinder.AssemblyFindPaths.Add(EDDOptions.Instance.DLLAppDirectory());      // any needed assemblies from here
            var dllexe = EDDOptions.Instance.DLLExeDirectory();     // and possibly from here, may not be present
            if (dllexe != null)
                EliteDangerousCore.DLL.EDDDLLAssemblyFinder.AssemblyFindPaths.Add(dllexe);
            AppDomain.CurrentDomain.AssemblyResolve += EliteDangerousCore.DLL.EDDDLLAssemblyFinder.AssemblyResolve;

            DLLManager = new EliteDangerousCore.DLL.EDDDLLManager();
            DLLCallBacks = new EDDDLLInterfaces.EDDDLLIF.EDDCallBacks();
            DLLCallBacks.ver = 3;       // explicitly,this is what we do
            System.Diagnostics.Debug.Assert(DLLCallBacks.ver == EDDDLLInterfaces.EDDDLLIF.DLLCallBackVersion, "***** Updated EDD DLL IF but not updated callbacks");
            DLLCallBacks.RequestHistory = DLLRequestHistory;
            DLLCallBacks.RunAction = DLLRunAction;
            DLLCallBacks.GetShipLoadout = DLLGetShipLoadout;
            DLLCallBacks.WriteToLog = (s) => LogLine(s);
            DLLCallBacks.WriteToLogHighlight = (s) => LogLineHighlight(s);
            DLLCallBacks.RequestScanData = DLLRequestScanData;
            DLLCallBacks.GetSuitsWeaponsLoadouts = DLLGetSuitWeaponsLoadout;
            DLLCallBacks.GetCarrierData = DLLGetCarrierData;
            DLLCallBacks.GetVisitedList = DLLGetVisitedList;
            DLLCallBacks.GetShipyards = DLLGetShipyards;
            DLLCallBacks.GetOutfitting = DLLGetOutfitting;
            DLLCallBacks.GetTarget = DLLGetTarget;
            DLLCallBacks.AddPanel = (id, paneltype, wintitle, refname, description, image) =>
            {
                // registered panels, search the stored list, see if there, then it gets the index, else its added to the list
                string regpanelstr = EliteDangerousCore.DB.UserDatabase.Instance.GetSettingString("DLLUserPanelsRegisteredList", "");
                string splitstr = "\u2737";
                string[] registeredpanels = regpanelstr.Split(splitstr, emptyarrayifempty: true);
                int indexof = Array.IndexOf(registeredpanels, id);
                int panelid = PanelInformation.DLLUserPanelsStart + (indexof < 0 ? registeredpanels.Length : indexof);       // set panel id
                if (indexof == -1)
                {
                    EliteDangerousCore.DB.UserDatabase.Instance.PutSettingString("DLLUserPanelsRegisteredList", regpanelstr.AppendPrePad(id, splitstr));
                }

                // IF we had more versions of IEDDPanelExtensions in future, we would add more clauses here and have other UserControlExtPanel classes to handle them

                if (typeof(EDDDLLInterfaces.EDDDLLIF.IEDDPanelExtension).IsAssignableFrom(paneltype))
                {
                    System.Diagnostics.Trace.WriteLine($"DLL added panel for IEDDPanelExtension and UserControlExtPanel: {id} {paneltype.Name} {wintitle} {refname} {description} {panelid}");
                    PanelInformation.AddPanel(panelid, typeof(UserControls.UserControlExtPanel), paneltype, wintitle, refname, description, image);
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"***** DLL unknown panel interface type - ignoring {id}");
                }

                UpdatePanelListInContextMenuStrip();
                OnPanelAdded?.Invoke();
            };
            dllsalloweddisallowed = EDDConfig.Instance.DLLPermissions;
            dllresults = DLLStart(ref dllsalloweddisallowed);       // we run it, and keep the results for processing in Shown
        }


        public bool DLLRunAction(string eventname, string paras)
        {
            System.Diagnostics.Debug.WriteLine("Run " + eventname + "(" + paras + ")");
            actioncontroller.ActionRun(Actions.ActionEventEDList.DLLEvent(eventname), new BaseUtils.Variables(paras, BaseUtils.Variables.FromMode.MultiEntryComma));
            return true;
        }

        private bool DLLRequestHistory(long index, bool isjid, out EDDDLLInterfaces.EDDDLLIF.JournalEntry f)
        {
            HistoryEntry he = isjid ? History.GetByJID(index) : History.GetByEntryNo((int)index);
            f = EliteDangerousCore.DLL.EDDDLLCallerHE.CreateFromHistoryEntry(History, he);
            return he != null;
        }
        private string DLLGetShipLoadout(string name)
        {
            if ( name.EqualsIIC("All"))
            {
                JObject ships = new JObject();
                int index = 0;
                foreach( var sh in History.ShipInformationList.Ships)
                {
                    ships[index++.ToStringInvariant()] = JToken.FromObject(sh.Value, true, new Type[] { }, 5, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                }

                //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllshiploadout.json", ships.ToString(true));
                return ships.ToString();
            }
            else
            {
                var sh = !name.IsEmpty() ? History.ShipInformationList.GetShipByFullInfoMatch(name) : History.ShipInformationList.CurrentShip;
                var ret = sh != null ? JToken.FromObject(sh, true, new Type[] { }, 5, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).ToString(true) : null;
                //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllshiploadout.json", ret);
                return ret;
            }
        }

        private string DLLGetTarget()
        {
            var hastarget = EliteDangerousCore.DB.TargetClass.GetTargetPosition(out string name, out double x, out double y, out double z);
            JObject jo = hastarget ? new JObject() { ["Name"] = name, ["X"] = x, ["Y"] = y, ["Z"] = z } : new JObject();
            return jo.ToString();
        }

        // Note ASYNC so we must use data return method
        private async void DLLRequestScanData(object requesttag, object usertag, string systemname, bool edsmlookup)           
        {
            var dll = DLLManager.FindCSharpCallerByStackTrace();    // need to find who called - use the stack to trace the culprit

            if (dll != null)
            {
                var syslookup = systemname.IsEmpty() ? History.CurrentSystem()?.Name : systemname;      // get a name

                JToken json = new JObject();     // default return

                if (syslookup.HasChars())
                {
                    var sc = History.StarScan;
                    var snode = await sc.FindSystemAsync(new SystemClass(syslookup), edsmlookup);       // async lookup
                    if (snode != null)
                        json = JToken.FromObject(snode, true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                }

                //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllscan.json", json.ToString(true));
                dll.DataResult(requesttag, usertag, json.ToString());
            }

        }


        private string DLLGetSuitWeaponsLoadout()
        {
            var wlist = JToken.FromObject(History.WeaponList.Weapons.Get(History.GetLast?.Weapons ?? 0), true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var slist = JToken.FromObject(History.SuitList.Suits(History.GetLast?.Suits ?? 0), true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var sloadoutlist = JToken.FromObject(History.SuitLoadoutList.Loadouts(History.GetLast?.Loadouts ?? 0), true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            JObject ret = new JObject();
            ret["Weapons"] = wlist;
            ret["Suits"] = slist;
            ret["Loadouts"] = sloadoutlist;
            //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllsuits.json", ret.ToString(true));
            return ret.ToString();
        }

        private string DLLGetCarrierData()
        {
            var carrier = JToken.FromObject(History.Carrier, true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllcarrier.json", carrier.ToString(true));
            return carrier.ToString();
        }

        private class VisitedSystem     // for JSON export
        {
            public string Name;
            public double X; public double Y; public double Z;
            public long SA;
            public VisitedSystem(string n,long addr,double x,double y,double z) { Name = n;SA = addr;X = x;Y = y;Z = z; }
        };

        private string DLLGetVisitedList(int howmany)
        {
            var list = History.Visited.Values;
            int toskip = howmany > list.Count || howmany < 0 ? list.Count : list.Count-howmany;
            var vlist = list.Skip(toskip).Select(x => new VisitedSystem(x.System.Name,x.System.SystemAddress??-1,x.System.X,x.System.Y,x.System.Z)).ToList();
            var visited = JToken.FromObject(vlist, true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var str = visited.ToString();
            //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllvisited.json", str);
            return str;
        }
        private string DLLGetShipyards()
        {
            var shipyards = JToken.FromObject(History.Shipyards, true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dllshipyards.json", shipyards.ToString(true));
            return shipyards.ToString();
        }
        private string DLLGetOutfitting()
        {
            var outfitting = JToken.FromObject(History.Outfitting, true, new Type[] { typeof(System.Drawing.Image) }, 12, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            //BaseUtils.FileHelpers.TryWriteToFile(@"c:\code\dlloutfitting.json", outfitting.ToString(true));
            return outfitting.ToString();
        }


        public Tuple<string, string, string, string> DLLStart(ref string alloweddlls)
        {
            System.Diagnostics.Trace.WriteLine(BaseUtils.AppTicks.TickCountLap() + " Load DLL");

            string verstring = EDDApplicationContext.AppVersion;
            string[] options = new string[] { EDDDLLInterfaces.EDDDLLIF.FLAG_HOSTNAME + "EDDiscovery",
                                              EDDDLLInterfaces.EDDDLLIF.FLAG_JOURNALVERSION + EliteDangerousCore.DLL.EDDDLLCallerHE.JournalVersion.ToStringInvariant(), 
                                              EDDDLLInterfaces.EDDDLLIF.FLAG_CALLBACKVERSION + DLLCallBacks.ver.ToStringInvariant(),
                                              EDDDLLInterfaces.EDDDLLIF.FLAG_CALLVERSION + EliteDangerousCore.DLL.EDDDLLCaller.DLLCallerVersion.ToStringInvariant(),
                                              EDDDLLInterfaces.EDDDLLIF.FLAG_PANELCALLBACKVERSION + UserControls.UserControlExtPanel.PanelCallBackVersion.ToStringInvariant(),
                                            };

            string[] dllpaths = new string[] { EDDOptions.Instance.DLLAppDirectory(), EDDOptions.Instance.DLLExeDirectory() };
            bool[] autodisallow = new bool[] { false, true };
            return DLLManager.Load(dllpaths, autodisallow, verstring, options, DLLCallBacks, ref alloweddlls,
                                                             (name) => UserDatabase.Instance.GetSettingString("DLLConfig_" + name, ""), (name, set) => UserDatabase.Instance.PutSettingString("DLLConfig_" + name, set));
        }


    }
}
