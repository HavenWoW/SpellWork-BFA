using SpellWork.Database;
using SpellWork.Properties;
using System;
using System.Windows.Forms;

namespace SpellWork.Forms
{
    public partial class FormSettings : Form
    {
        public FormSettings()
        {
            InitializeComponent();
        }

        private void CbUseDbConnectCheckedChanged(object sender, EventArgs e)
        {
            _gbDbSetting.Enabled = ((CheckBox)sender).Checked;
            this._bTestConnect.Enabled = _gbDbSetting.Enabled;
        }

        private void BSaveSettingsClick(object sender, EventArgs e)
        {
            Settings.Default.Host = _tbHost.Text;
            Settings.Default.PortOrPipe = _tbPort.Text;
            Settings.Default.User = _tbUser.Text;
            Settings.Default.Pass = _tbPass.Text;
            Settings.Default.WorldDbName = _tbBase.Text;
            Settings.Default.UseDbConnect = _cbUseDBConnect.Checked;
            Settings.Default.DbcPath = _tbPath.Text;
            Settings.Default.GtPath = _tbGtPath.Text;
            Settings.Default.Locale = _tbLocale.Text;

            var connected = new WorldDatabase(Program.BuildConfig()).TestConnection(out var error);

            if (error != null)
                MessageBox.Show(error);

            if (((Button)sender).Text != @"Save" && connected)
                MessageBox.Show(@"Connection successful!", @"MySQL Connections!", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (((Button)sender).Text != @"Save")
                return;

            Settings.Default.Save();
            Close();

            Application.Restart();
        }

        private void SettingsFormLoad(object sender, EventArgs e)
        {
            _tbHost.Text = Settings.Default.Host;
            _tbPort.Text = Settings.Default.PortOrPipe;
            _tbUser.Text = Settings.Default.User;
            _tbPass.Text = Settings.Default.Pass;
            _tbBase.Text = Settings.Default.WorldDbName;
            _gbDbSetting.Enabled = _cbUseDBConnect.Checked = Settings.Default.UseDbConnect;
            _tbPath.Text = Settings.Default.DbcPath;
            _tbGtPath.Text = Settings.Default.GtPath;
            _tbLocale.Text = Settings.Default.Locale;
        }

        private void FormSettings_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        }

        private void _tbPathClick(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Default.DbcPath = folderBrowserDialog1.SelectedPath;
                Settings.Default.Save();
            }
        }
    }
}
