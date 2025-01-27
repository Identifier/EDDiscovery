﻿/*
 * Copyright 2019-2021 Robbyxp1 @ github.com
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

using EDDiscovery.UserControls.Map3D;
using EliteDangerousCore;
using GLOFC.WinForm;
using System;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlLocal3DMap : UserControlCommonBase
    {
        private GLWinFormControl glwfc;
        private Timer systemtimer = new Timer();
        private Map map;
        private UserControl3DMap.MapSaverImpl mapsave;

        public UserControlLocal3DMap() 
        {
            InitializeComponent();
        }

        public override void Init()
        {
            DBBaseName = "Local3DMapPanel_";

            glwfc = new GLOFC.WinForm.GLWinFormControl(panelOuter,null,4,6);
            glwfc.EnsureCurrent = true;      // set, ensures context is set up for internal code on paint and any Paints chained to it
        }

        public override void LoadLayout()
        {
            base.LoadLayout();

            glwfc.EnsureCurrentContext();

            // load setup restore settings of map
            map = new Map();

            if (map.Start(glwfc, DiscoveryForm.GalacticMapping, DiscoveryForm.EliteRegions, this,
                  Map.Parts.None
                | Map.Parts.Galaxy
                | Map.Parts.Grid | Map.Parts.TravelPath | Map.Parts.NavRoute
                | Map.Parts.EDSMStars
                | Map.Parts.PrepopulateEDSMLocalArea
                | Map.Parts.Menu
                | Map.Parts.SearchBox | Map.Parts.RightClick
                | Map.Parts.YHoldButton | Map.Parts.GalaxyResetPos
                | Map.Parts.Bookmarks
                )
                )
            {
                mapsave = new UserControl3DMap.MapSaverImpl(this);
                map.LoadState(mapsave, true, 200000);

                map.UpdateEDSMStarsLocalArea();    // now try and ask for a populated update after loading the settings

                map.AddSystemsToExpedition = (list) =>
                {
                    RequestPanelOperation?.Invoke(this, new UserControlCommonBase.PushStars() { PushTo = UserControlCommonBase.PushStars.PushType.Expedition, Systems = list });
                };

                // start clock
                systemtimer.Interval = 50;
                systemtimer.Tick += new EventHandler(SystemTick);
                systemtimer.Start();

                DiscoveryForm.OnHistoryChange += Discoveryform_OnHistoryChange;
                DiscoveryForm.OnNewEntry += Discoveryform_OnNewEntry;
                DiscoveryForm.OnSyncComplete += Discoveryform_OnSyncComplete;
            }
        }

        public override void Closing()
        {
            System.Diagnostics.Debug.WriteLine($"local 3dmap {DisplayNumber} stop");

            if (map != null)    // just in case loadlayout has not been called..
            {
                systemtimer.Stop();

                DiscoveryForm.OnHistoryChange -= Discoveryform_OnHistoryChange;
                DiscoveryForm.OnNewEntry -= Discoveryform_OnNewEntry;
                DiscoveryForm.OnSyncComplete -= Discoveryform_OnSyncComplete;

                glwfc.EnsureCurrentContext();           // must make sure current context before we call all the dispose functions
                map.SaveState(mapsave);
                map.Dispose();
            }

            glwfc.Dispose();
            glwfc = null;
        }

        private void SystemTick(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.Assert(systemtimer.Enabled);

            glwfc.EnsureCurrentContext();           // ensure the context
            GLOFC.Utils.PolledTimer.ProcessTimers();     // work may be done in the timers to the GL.
            map.Systick();
        }

        private void Discoveryform_OnNewEntry(HistoryEntry he)
        {
            glwfc.EnsureCurrentContext();           // ensure the context

            if (he.IsFSDCarrierJump)
            {
                map.UpdateTravelPath();
            }
            else if (he.journalEntry.EventTypeID == JournalTypeEnum.NavRoute)
            {
                map.UpdateNavRoute();
            }

        }

        private void Discoveryform_OnSyncComplete(long full, long update)
        {
            glwfc.EnsureCurrentContext();           // ensure the context

            if (full + update > 0)      // only if something changes do we refresh
            {
                map.UpdateEDSMStarsLocalArea();
            }
        }

        private void Discoveryform_OnHistoryChange()
        {
            glwfc.EnsureCurrentContext();           // ensure the context

            map.UpdateEDSMStarsLocalArea();
            map.UpdateTravelPath();
            map.UpdateNavRoute();
        }
    }
}
