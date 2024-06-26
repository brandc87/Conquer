using System.Threading.Tasks;
using System.Windows.Forms;
using GameServer.Database;

namespace GameServer;

public sealed class 整理数据 : GMCommand
{
    public override ExecuteCondition Priority => ExecuteCondition.Inactive;
    public override UserDegree Degree => UserDegree.SysOp;

    public override void ExecuteCommand()
    {
        if (MessageBox.Show("Organising data requires reordering all customer data to save ID resources\r\n\r\nThis operation is irreversible, please make a good backup of your data\r\n\r\nAre you sure you want to do this?", "Dangerous operation", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.OK)
        {
            return;
        }
        SMain.AddCommandLog("<= @" + GetType().Name + " Start executing the command, do not close the window during the process.");
        SMain.Main.BeginInvoke(() =>
        {
            SMain.Main.SettingsPage.Enabled = false;
            SMain.Main.下方控件页.Enabled = false;
        });
        Task.Run(delegate
        {
            Session.Save(commit: true);
            SMain.Main.BeginInvoke(() =>
            {
                SMain.Main.SettingsPage.Enabled = true;
                SMain.Main.下方控件页.Enabled = true;
            });
        });
    }
}
