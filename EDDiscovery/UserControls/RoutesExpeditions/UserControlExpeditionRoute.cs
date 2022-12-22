﻿/*
 * Copyright © 2016 - 2022 EDDiscovery development team
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
using EliteDangerousCore.DB;
using ExtendedControls;
using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlExpedition
    {
        #region Routes

        // if the data in the grid is set, and different to the loadedroute, or the grid is not empty.  
        // not dirty if the grid is empty (name and systems empty)
        public bool IsDirty()
        {
            var gridroute = CopyGridIntoRoute();
            return (gridroute != null && loadedroute != null) ? !gridroute.Equals(loadedroute) : gridroute != null;
        }

        // move systems in grid into this route class
        private SavedRouteClass CopyGridIntoRoute()
        {
            SavedRouteClass route = new SavedRouteClass();
            route.Name = textBoxRouteName.Text.Trim();

            route.Systems.Clear();

            var data = dataGridView.Rows.OfType<DataGridViewRow>()
                .Where(r => r.Index < dataGridView.NewRowIndex && r.Cells[0].Value != null)
                .Select(r => new SavedRouteClass.SystemEntry(r.Cells[0].Value as string, r.Cells[1].Value as string));

            route.Systems.AddRange(data);

            if (dateTimePickerStartDate.Checked)
            {
                route.StartDateUTC = EDDConfig.Instance.ConvertTimeToUTCFromPicker(dateTimePickerStartDate.Value.Date);
                if (dateTimePickerStartTime.Checked)
                    route.StartDateUTC += dateTimePickerStartTime.Value.TimeOfDay;
            }
            else
            {
                route.StartDateUTC = null;
            }

            if (dateTimePickerEndDate.Checked)
            {
                route.EndDateUTC = EDDConfig.Instance.ConvertTimeToUTCFromPicker(dateTimePickerEndDate.Value.Date);
                route.EndDateUTC += dateTimePickerEndTime.Checked ? dateTimePickerEndTime.Value.TimeOfDay : new TimeSpan(23, 59, 59);
            }
            else
            {
                route.EndDateUTC = null;
            }

            return route.Systems.Count > 0 || route.Name.HasChars() ? route : null;           // if systems or name, there is a route
        }

        // Move the current route into the DB
        private bool StoreCurrentRouteIntoDB(SavedRouteClass newrt)
        {
            if (newrt.Name.IsEmpty())
            {
                ExtendedControls.MessageBoxTheme.Show(FindForm(), "Please specify a name for the route.".T(EDTx.UserControlExpedition_Specify), "Warning".T(EDTx.Warning), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }

            var savedroutes = SavedRouteClass.GetAllSavedRoutes();
            var edsmroute = savedroutes.Find(x => x.Name.Equals(newrt.Name, StringComparison.InvariantCultureIgnoreCase) && x.EDSM);

            if (edsmroute != null)
            {
                ExtendedControls.MessageBoxTheme.Show(FindForm(), ("The current route name conflicts with a well-known expedition." + Environment.NewLine
                    + "Please specify a new name to save your changes.").T(EDTx.UserControlExpedition_Conflict), "Warning".T(EDTx.Warning), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                return false;
            }

            var overwriteroute = savedroutes.Where(r => r.Name.Equals(newrt.Name)).FirstOrDefault();

            if (overwriteroute != null)
            {
                if (MessageBoxTheme.Show(FindForm(), "Warning: route already exists. Would you like to overwrite it?".T(EDTx.UserControlExpedition_Overwrite), "Warning".T(EDTx.Warning), MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return false;

                overwriteroute.Delete();
            }

            if (overwriteroute == null)
                return newrt.Add();
            else
            {
                newrt.Id = overwriteroute.Id;
                return newrt.Add();
            }
        }

        // true if grid is empty, or it has saved
        private bool SaveGrid()
        {
            SavedRouteClass route = CopyGridIntoRoute();
            if (route != null)
            {
                if (StoreCurrentRouteIntoDB(route))
                {
                    loadedroute = route;
                    return true;
                }
                else
                    return false;
            }
            else
                return true;
        }

        private bool PromptAndSaveIfNeeded()
        {
            if (IsDirty())
            {
                var result = ExtendedControls.MessageBoxTheme.Show(FindForm(), ("Expedition - There are unsaved changes to the current route." + Environment.NewLine
                    + "Would you like to save the current route before proceeding?").T(EDTx.UserControlExpedition_Unsaved), "Warning".T(EDTx.Warning), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);

                switch (result)
                {
                    case DialogResult.Yes:
                        return SaveGrid();

                    case DialogResult.No:
                        return true;

                    case DialogResult.Cancel:
                    default:
                        return false;
                }
            }
            else
                return true;
        }


        #endregion

    }
}
