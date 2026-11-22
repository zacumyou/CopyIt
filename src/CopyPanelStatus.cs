namespace CopyIt
{
    public partial class CopyTool
    {
        private string panelStatusSource,panelStatusCache;
        private static readonly string[] RoutineStatusKeys={"CopyIt.Text.F6EA647C2C","CopyIt.Text.25331ED127","CopyIt.Text.04673003E3","CopyIt.Text.6A4538F5AA","CopyIt.Text.9AC3A0BEBB","CopyIt.Text.22D93553CC","CopyIt.Text.DC7E9F0280","CopyIt.Text.BAD401D904","CopyIt.Text.F6A4B5B56A","CopyIt.Text.9600604679"};
        internal string PanelStatus
        {
            get {
                if(panelStatusSource==Status)return panelStatusCache;
                panelStatusSource=Status;panelStatusCache=Status;
                foreach(var key in RoutineStatusKeys)if(Status==L10n.Message(key)){panelStatusCache="";break;}
                if(Status==L10n.Message("CopyIt.Text.870C5F4972",selected.Count)||Status==L10n.Message("CopyIt.Text.66537D0FF9",samples.Count))panelStatusCache="";
                return panelStatusCache;
            }
        }
    }
}
