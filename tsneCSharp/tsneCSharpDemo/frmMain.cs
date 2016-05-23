using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using tsneCSharp;

namespace tsneCSharpDemo
{
    public partial class frmMain : Form
    {
        private tSNE tsne;
        private string[] srcRowHeader;
        private double[][] srcDataSet;
        private double[][] result2D;

        private List<Point> points = new List<Point>();

        public frmMain()
        {
            InitializeComponent();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            double perplexity;
            int dimension;
            double epsilon;

            if (Double.TryParse(tbPerplexity.Text, out perplexity) &&
                Int32.TryParse(tbDimension.Text, out dimension) &&
                Double.TryParse(tbEpsilon.Text, out epsilon) &&
                this.srcDataSet != null)
            {
                this.tsne = new tSNE(perplexity, dimension, epsilon);
                this.tsne.InitDataRaw(this.srcDataSet);

                for (int i = 0; i < 500; i++)
                {
                    tsne.Step();
                }

                this.result2D = tsne.GetSolution();
                this.DrawResult();
            }
            else
            {
                MessageBox.Show("Invalid Option");
            }            
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbSource.Text = dlg.FileName;
                this.SetData(dlg.FileName);
            }
        }

        private void SetData(string fileName)
        {
            string line = string.Empty;
            string[] token;
            int n;
            char delimiter = Char.Parse(this.tbDelimiter.Text);

            List<string> header = new List<string>();
            List<double[]> rowData = new List<double[]>();

            StreamReader reader = new StreamReader(fileName);
            
            // read each row
            while ((line = reader.ReadLine()) != null)
            {
                token = line.Split(delimiter);
                n = token.Length;

                string[] data = new string[n - 1];                
                Array.Copy(token, 1, data, 0, n - 1);
                double[] dData = Array.ConvertAll(data, Double.Parse);

                header.Add(token[0]);
                rowData.Add(dData);
            }

            this.srcRowHeader = header.ToArray();
            this.srcDataSet = new double[rowData.Count][];
            for (int i = 0; i < rowData.Count; i++)
            {
                this.srcDataSet[i] = rowData[i];
            }

            label4.Text = header.Count.ToString() + " Data has been loaded.";
        }

        private void DrawResult()
        {
            double scale = 100;

            int width = this.pictureBox1.Width;
            int height = this.pictureBox1.Height;
            int side = Math.Min(width, height);
            int mean = side / 2;

            this.points = new List<Point>();
            for (int i = 0; i < result2D.Length; i++)
            {
                int x = (int)((result2D[i][0] * scale) + mean);
                int y = (int)((result2D[i][1] * scale) + mean);

                this.points.Add(new Point(x, y));
            }
            
            pictureBox1.Invalidate();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (points.Count > 0)
            {
                int n = 0;

                foreach (Point p in this.points)
                {
                    e.Graphics.FillRectangle(Brushes.Blue, p.X, p.Y, 2, 2);
                    e.Graphics.DrawString(srcRowHeader[n], this.Font, Brushes.Black, p.X, p.Y);
                    n++;
                }
            }
        }
    }
}
