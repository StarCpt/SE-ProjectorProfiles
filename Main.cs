using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace SE_ProjectorAlignmentProfiles
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Projector), false)]
    public class ProjectorLogic : MyGameLogicComponent
    {
        IMyProjector block;
        static bool controlsAdded = false;
        AlignData? _selected;
        AlignData? SelectedProfile
        {
            get { return _selected; }
            set
            {
                _selected = value;
                loadProfileBtn.UpdateVisual();
            }
        }
        StringBuilder newProfileNameStrb = new StringBuilder();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if ((Entity as IMyProjector)?.CubeGrid?.Physics != null)
            {
                block = Entity as IMyProjector;
                if (!controlsAdded)
                {
                    controlsAdded = true;
                    AddControls();
                }
            }
        }

        static IMyTerminalControlListbox profilesListbox;
        static IMyTerminalControlButton loadProfileBtn;
        static IMyTerminalControlTextbox profileNameTextbox;
        static IMyTerminalControlButton saveProfileBtn;
        static void AddControls()
        {
            profilesListbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyProjector>("ProfilesListbox");
            profilesListbox.Enabled = b => ((IMyProjector)b).IsProjecting;
            profilesListbox.SupportsMultipleBlocks = false;
            profilesListbox.Multiselect = false;
            profilesListbox.Title = MyStringId.GetOrCompute("Profiles");
            profilesListbox.VisibleRowsCount = 5;
            profilesListbox.ListContent = (b, items, selected) => b.GameLogic.GetAs<ProjectorLogic>().ListContent(items, selected);
            profilesListbox.ItemSelected = (b, selected) => b.GameLogic.GetAs<ProjectorLogic>().ItemSelected(selected);
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(profilesListbox);

            loadProfileBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("LoadProfileBtn");
            loadProfileBtn.Enabled = b => ((IMyProjector)b).IsProjecting && b.GameLogic.GetAs<ProjectorLogic>().SelectedProfile.HasValue;
            loadProfileBtn.SupportsMultipleBlocks = false;
            loadProfileBtn.Title = MyStringId.GetOrCompute("Load Profile");
            loadProfileBtn.Action = b => b.GameLogic.GetAs<ProjectorLogic>().ApplyAlignment();
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(loadProfileBtn);

            profileNameTextbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyProjector>("ProfileNameTextbox");
            profileNameTextbox.Enabled = b => ((IMyProjector)b).IsProjecting;
            profileNameTextbox.SupportsMultipleBlocks = false;
            profileNameTextbox.Title = MyStringId.GetOrCompute("New Profile Name");
            profileNameTextbox.Getter = b => b.GameLogic.GetAs<ProjectorLogic>().newProfileNameStrb;
            profileNameTextbox.Setter = (b, strb) =>
            {
                b.GameLogic.GetAs<ProjectorLogic>().newProfileNameStrb = strb;
                saveProfileBtn.UpdateVisual();
            };
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(profileNameTextbox);

            saveProfileBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("SaveProfileBtn");
            saveProfileBtn.Enabled = b => ((IMyProjector)b).IsProjecting && !string.IsNullOrWhiteSpace(b.GameLogic.GetAs<ProjectorLogic>().newProfileNameStrb.ToString());
            saveProfileBtn.SupportsMultipleBlocks = false;
            saveProfileBtn.Title = MyStringId.GetOrCompute("Save Profile");
            saveProfileBtn.Tooltip = MyStringId.GetOrCompute("Save current projector settings");
            saveProfileBtn.Action = b => b.GameLogic.GetAs<ProjectorLogic>().SaveAlignment();
            MyAPIGateway.TerminalControls.AddControl<IMyProjector>(saveProfileBtn);
        }

        void ListContent(List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            string[] lines = block.CustomData.Split(Environment.NewLine.ToCharArray());
            bool match = false;
            foreach (string line in lines)
            {
                AlignData data;
                if (!AlignData.TryParse(line, out data))
                    continue;

                var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(data.Name), MyStringId.GetOrCompute(data.GetToolTip()), data);
                items.Add(item);

                if (!match && this.SelectedProfile.HasValue && this.SelectedProfile.Value.Equals(data))
                {
                    match = true;
                    selected.Add(item);
                }
            }

            if (!match)
            {
                this.SelectedProfile = null;
            }
        }

        void ItemSelected(List<MyTerminalControlListBoxItem> selected)
        {
            this.SelectedProfile = (AlignData)selected[0].UserData;
        }

        void ApplyAlignment()
        {
            if (SelectedProfile.HasValue)
            {
                block.ProjectionOffset = SelectedProfile.Value.Offset;
                block.ProjectionRotation = SelectedProfile.Value.Rotation;
                block.UpdateOffsetAndRotation();
            }
        }

        void SaveAlignment()
        {
            string newProfileNameStr = newProfileNameStrb.ToString();
            if (string.IsNullOrWhiteSpace(newProfileNameStr))
                return;

            StringBuilder newCD = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(block.CustomData))
            {
                string[] lines = block.CustomData.Split(Environment.NewLine.ToCharArray());
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].StartsWith($"AlignProfile:{newProfileNameStr}") && lines[i] != "")
                    {
                        newCD.AppendLine(lines[i]);
                    }
                }
            }
            newCD.Append(new AlignData(newProfileNameStr, block.ProjectionOffset, block.ProjectionRotation).ToString());
            block.CustomData = newCD.ToString();

            newProfileNameStrb.Clear();
            profilesListbox.UpdateVisual();
            profileNameTextbox.UpdateVisual();
            saveProfileBtn.UpdateVisual();
        }
    }

    public struct AlignData
    {
        public string Name { get; }
        public Vector3I Offset { get; }
        public Vector3I Rotation { get; }

        public AlignData(string name, Vector3I offset, Vector3I rotation)
        {
            this.Name = name;
            this.Offset = offset;
            this.Rotation = rotation;
        }

        public static bool TryParse(string str, out AlignData result)
        {
            result = default(AlignData);

            if (string.IsNullOrWhiteSpace(str) || !str.StartsWith("AlignProfile:"))
                return false;

            string[] args = str.Remove(0, 13).Split('/');
            if (args.Length != 3)
                return false;

            Vector3I off;
            Vector3I rot;

            if (!Vector3I.TryParseFromString(args[1], out off) | !Vector3I.TryParseFromString(args[2], out rot))
                return false;

            result = new AlignData(args[0], off, rot);
            return true;
        }

        public override string ToString()
        {
            return $"AlignProfile:{Name}/{Offset.GetParsableString()}/{Rotation.GetParsableString()}";
        }

        public string GetToolTip()
        {
            return $"X:{Offset.X} Y:{Offset.Y} Z:{Offset.Z} Pitch:{Rotation.X * 90} Yaw:{Rotation.Y * 90} Roll:{Rotation.Z * 90}";
        }
    }

    public static class Extensions
    {
        public static string GetParsableString(this Vector3I vec)
        {
            return $"{vec.X};{vec.Y};{vec.Z}";
        }
    }
}
