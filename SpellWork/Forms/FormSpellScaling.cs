using SpellWork.DBC.Structures;
using System;
using System.Data;
using System.Windows.Forms;

namespace SpellWork.Forms
{
    public partial class FormSpellScaling : Form
    {
        public uint SelectedLevel => (uint)_tbLevel.Value;
        public uint SelectedItemLevel => (uint)_tbItemLevel.Value;
        public MapDifficultyEntry SelectedMapDifficulty => _cbDifficulty.SelectedValue is MapDifficultyEntry mapDifficulty ? mapDifficulty : null;

        public FormSpellScaling()
        {
            InitializeComponent();
            InitializeMapComboBox();
            InitializeDifficultyComboBox();

            if (DBC.DBC.SelectedMapDifficulty != null)
            {
                _cbMap.SelectedValue = DBC.DBC.Map[(int)DBC.DBC.SelectedMapDifficulty.MapID];
                _cbDifficulty.SelectedValue = DBC.DBC.SelectedMapDifficulty;
            }
        }

        private void InitializeMapComboBox()
        {
            var dt = new DataTable();
            dt.Columns.Add("Value", typeof(MapEntry));
            dt.Columns.Add("ID", typeof(uint));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Display", typeof(string)).Expression = "ID + ' - ' + Name";

            dt.Rows.Add();

            foreach (var map in DBC.DBC.Map.Values)
            {
                var row = dt.NewRow();
                row["Value"] = map;
                row["ID"] = map.ID;
                row["Name"] = map.MapName;
                dt.Rows.Add(row);
            }

            _mapDataBindingSource.DataSource = dt;
            _cbMap.DisplayMember = "Display";
            _cbMap.ValueMember = "Value";
        }

        private void InitializeDifficultyComboBox()
        {
            var dt = new DataTable();
            dt.Columns.Add("Value", typeof(MapDifficultyEntry));
            dt.Columns.Add("MapID", typeof(uint));
            dt.Columns.Add("DifficultyID", typeof(uint));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Display", typeof(string)).Expression = "DifficultyID + ' - ' + Name";

            dt.Rows.Add();

            foreach (var difficulty in DBC.DBC.MapDifficulty.Values)
            {
                if (!DBC.DBC.Difficulty.ContainsKey(difficulty.DifficultyID))
                    continue;

                var row = dt.NewRow();
                row["Value"] = difficulty;
                row["MapID"] = difficulty.MapID;
                row["DifficultyID"] = difficulty.DifficultyID;
                row["Name"] = DBC.DBC.Difficulty[difficulty.DifficultyID].Name;
                dt.Rows.Add(row);
            }

            _difficultyDataBindingSource.DataSource = dt;
            _cbDifficulty.DisplayMember = "Display";
            _cbDifficulty.ValueMember = "Value";
        }

        private void LevelValueChanged(object sender, EventArgs e)
        {
            _tbxLevel.Text = ((TrackBar)sender).Value.ToString();
        }

        private void ItemLevelValueChanged(object sender, EventArgs e)
        {
            _tbxItemLevel.Text = ((TrackBar)sender).Value.ToString();
        }

        private void LevelTextKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != (char)Keys.Delete && e.KeyChar != (char)Keys.Escape && e.KeyChar != (char)Keys.Enter)
                e.Handled = true;
        }

        private void LevelTextChanged(object sender, EventArgs e)
        {
            var tb = (TextBox)sender;
            if (tb.Text.Length <= 0)
            {
                _tbLevel.Value = 1;
                tb.Text = "1";
                return;
            }

            var val = uint.Parse(tb.Text);
            if (val > DBC.DBC.MaxLevel)
                tb.Text = DBC.DBC.MaxLevel.ToString();
            else if (val <= 0)
                tb.Text = "1";

            val = uint.Parse(tb.Text);
            _tbLevel.Value = (int)val;
        }

        private void ItemLevelTextChanged(object sender, EventArgs e)
        {
            var tb = (TextBox)sender;
            if (tb.Text.Length <= 0)
            {
                tb.Text = "1";
                return;
            }

            var val = uint.Parse(tb.Text);
            if (val > DBC.DBC.MaxItemLevel)
                tb.Text = DBC.DBC.MaxItemLevel.ToString();
            else if (val <= 0)
                tb.Text = "1";

            val = uint.Parse(tb.Text);
            _tbItemLevel.Value = (int)val;
        }

        private void MapSearchTextChanged(object sender, EventArgs e)
        {
            var text = ((TextBox)sender).Text;
            if (text.Length <= 0)
            {
                _mapDataBindingSource.Filter = "";
                return;
            }
            if (uint.TryParse(text, out var mapId))
                _mapDataBindingSource.Filter = $"Value IS NULL OR ID = {mapId}";
            else
                _mapDataBindingSource.Filter = $"Value IS NULL OR Name LIKE '%{EscapeFilter(text)}%'";
        }

        private void MapSelectionChanged(object sender, EventArgs e)
        {
            var cb = (ComboBox)sender;
            var filter = "Value IS NULL";
            var selectedMap = (MapEntry)cb.SelectedValue;
            if (selectedMap != null)
                filter += $" OR MapID = {selectedMap.ID}";

            _difficultyDataBindingSource.Filter = filter;
            _cbDifficulty.SelectedIndex = 0;
        }

        private string EscapeFilter(string text)
        {
            var escaped = "";
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '\'':
                        escaped += ch;
                        escaped += ch;
                        break;
                    case '[':
                    case ']':
                    case '%':
                    case '*':
                        escaped += '[';
                        escaped += ch;
                        escaped += ']';
                        break;
                    default:
                        escaped += ch;
                        break;
                }
            }

            return escaped;
        }
    }
}
