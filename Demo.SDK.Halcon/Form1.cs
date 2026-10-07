using AAAandSphinx.Vision.SDK.Halcon;

namespace Demo.SDK.Halcon
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        CameraHalcon cam = new CameraHalcon();
        private void bnreflush_Click(object sender, EventArgs e)
        {
            var cams = cam.GetCameraEnum();
            this.cbcamlist.DataSource = cams;


        }
    }
}
