using System.Windows.Forms;

namespace Networks.Panels
{
    partial class NetworkPanel
    {
        #region Component Designer generated code
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            ListNetwork = new ListView();
            lblTitle = new Label();

            Size = new Size(610, 450);

            // 
            // ListNetwork
            // 
            ListNetwork.Location = new Point(10, 60);
            ListNetwork.Size = new Size(600, 360);
            ListNetwork.BackColor = Color.White;
            ListNetwork.BorderStyle = BorderStyle.FixedSingle;
            ListNetwork.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            ListNetwork.FullRowSelect = true;
            ListNetwork.GridLines = true;
            ListNetwork.View = View.Details;
            ListNetwork.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            ListNetwork.MultiSelect = false;
            ListNetwork.HideSelection = false;


            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTitle.Location = new Point(10, 20);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(50, 25);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Network Details";

            this.Controls.Add(lblTitle);
            this.Controls.Add(ListNetwork);
        }

        #endregion

        private ListView ListNetwork;
        private System.Threading.Timer NetworkRefreshTimer;
        private System.Threading.Timer NetworkUpdateTimer;    
        private Label lblTitle;
    }
}
