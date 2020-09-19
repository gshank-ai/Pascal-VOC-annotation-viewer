using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace DrawRecByXML
{
    public partial class fmMain : Form
    {
        string[] images_Path;
        List<string>[] images_Label;
        int current_image_num = 0;
        Dictionary<string, int> lable_count = new Dictionary<string, int>();
        Dictionary<string, int> current_label = new Dictionary<string, int>();
        List <rec_info> list_rec_info = new List<rec_info>();
        bool hasText = true;

        private System.Drawing.Point MouseDownPoint = new System.Drawing.Point();//平移時滑鼠按下的位置
        private bool IsSelected = false;//滑鼠是否是按下狀態

        public fmMain()
        {
            InitializeComponent();
            this.pictureBox1.Size = this.panel2.Size;
            this.pictureBox1.MouseWheel += new MouseEventHandler(pictureBox1_MouseWheel);
        }

        //選擇Voc資料夾
        private void bt_selectDir_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;

            FolderBrowserDialog dir = new FolderBrowserDialog();
            if (dir.ShowDialog() == DialogResult.OK)
            {
                //確認子資料夾是否存在JPEGImages,Annotations,ImageSets
                if (!Directory.Exists(dir.SelectedPath + "/JPEGImages") || !Directory.Exists(dir.SelectedPath + "/Annotations") || !Directory.Exists(dir.SelectedPath + "/ImageSets"))
                {
                    MessageBox.Show("請確認資料夾是否有誤!");
                    this.Cursor = Cursors.Default;
                    return;
                }

                try
                {
                    //清空資料dataGridView,lable_count,cb_Filter
                    dataGridView1.Rows.Clear();
                    lable_count = new Dictionary<string, int>();
                    cb_Filter.Items.Clear();
                    cb_Filter.Items.Add("ALL");
                    cb_Filter.SelectedItem = "ALL";
                    cb_Filter.Enabled = false;

                    //讀取所有照片路徑
                    images_Path = Directory.GetFiles(dir.SelectedPath + "/JPEGImages");

                    //建立儲存每張照片的Label資訊
                    images_Label = new List<string>[images_Path.Count()];

                    //一開始選第一張照片
                    current_image_num = 0;
                    //根據照片路徑,讀取相對的xml檔
                    showImage(images_Path[current_image_num]);

                    //計算各種label的數量,顯示在datagirdview
                    Task tGetLabelInfo = new Task(getLabelInfo);
                    tGetLabelInfo.Start();

                    tb_selectDirPath.Text = dir.SelectedPath;
                    lb_dsc.Text = String.Format("第1張,共{0}張", images_Path.Count());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    Console.WriteLine(ex.ToString());
                }
            }

            this.Cursor = Cursors.Default;
        }

        //取得資料集的所有ng資訊,更新至dataGridView
        private void getLabelInfo()
        {
            enabled_bt_selectDir(false);
            update_tabPage2("資料載入中..");

            for (int i = 0; i < images_Path.Count(); i++)
            {
                images_Label[i] = new List<string>();

                string xml_path = images_Path[i].Replace("JPEGImages", "Annotations").Replace(".jpg", ".xml");
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xml_path);//載入xml檔
                XmlNodeList xmlNL = xmlDoc.SelectNodes("/annotation/object");
                foreach (XmlNode node in xmlNL)
                {
                    XmlNodeList ChildNL = node.ChildNodes;
                    string label = getXmlNode(ChildNL, "name").InnerText;
                    if (lable_count.Keys.Contains(label))
                    {
                        lable_count[label] += 1;
                    }
                    else
                    {
                        lable_count[label] = 1;
                    }
                    images_Label[i].Add(label);
                }
            }

            update_dataGridView();

            update_cb_Filter();
            update_tabPage2("Label資訊");
            enabled_bt_selectDir(true);
        }

        //更改bt_selectDir的enabled
        public delegate void bInvokeDelegate(bool b);
        private void enabled_bt_selectDir(bool b)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new bInvokeDelegate(enabled_bt_selectDir), b);
                return;
            }

            bt_selectDir.Enabled = b;
        }

        //更改tabPage2的text
        public delegate void sInvokeDelegate(string s);
        private void update_tabPage2(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new sInvokeDelegate(update_tabPage2), text);
                return;
            }

            this.tabPage2.Text = text;
        }

        //更改dataGridView的資料
        public delegate void InvokeDelegate();
        private void update_dataGridView()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new InvokeDelegate(update_dataGridView));
                return;
            }

            foreach (string key in lable_count.Keys)
            {
                dataGridView1.Rows.Add(key, lable_count[key]);
            }
        }

        //更改cb_Filter的items
        private void update_cb_Filter()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new InvokeDelegate(update_cb_Filter));
                return;
            }
            foreach (string key in lable_count.Keys)
            {
                cb_Filter.Items.Add(key);
            }
            cb_Filter.Enabled = true;
        }

        //顯示照片並更新當前照片資訊
        private void showImage(string image_path)
        {
            //更新圖片
            string xml_path = image_path.Replace("JPEGImages", "Annotations").Replace(".jpg", ".xml");
            Image img = Image.FromFile(image_path);
            list_rec_info = get_current_rec_info(xml_path);
            drawRec(img, list_rec_info);//將圈選框組合後顯示在picturebox上

            //更新label2(各個label種數)
            label2.Text = "圖片資訊:\r\n";
            foreach (string key in current_label.Keys)
            {
                label2.Text += key + ":" + current_label[key].ToString() + "個\r\n";
            }

            //更新listBox1項目(各種label)
            listBox1.Items.Clear();
            listBox1.Items.Add(String.Format("圖片{0}:", Path.GetFileNameWithoutExtension(image_path)));
            foreach (rec_info item in list_rec_info)
            {
                listBox1.Items.Add(item.label);
            }
        }

        //label的敘述和位置
        class rec_info
        {
            public string label;
            public Rectangle rect;

            public rec_info(string label, Rectangle rect)
            {
                this.label = label;
                this.rect = rect;
            }
        }

        private List<rec_info> get_current_rec_info(string xml_path)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xml_path);//載入xml檔
            XmlNodeList xmlNL = xmlDoc.SelectNodes("/annotation/object");
            current_label = new Dictionary<string, int>();

            List<rec_info> list_recInfo = new List<rec_info>();

            foreach (XmlNode node in xmlNL)
            {
                XmlNodeList ChildNL = node.ChildNodes;
                string ng = getXmlNode(ChildNL, "name").InnerText;
                if (current_label.Keys.Contains(ng))
                {
                    current_label[ng] += 1;
                }
                else
                {
                    current_label[ng] = 1;
                }
                XmlNodeList recInfoNL = getXmlNode(ChildNL, "bndbox").ChildNodes;
                int xmin = Convert.ToInt32(getXmlNode(recInfoNL, "xmin").InnerText);
                int ymin = Convert.ToInt32(getXmlNode(recInfoNL, "ymin").InnerText);
                int xmax = Convert.ToInt32(getXmlNode(recInfoNL, "xmax").InnerText);
                int ymax = Convert.ToInt32(getXmlNode(recInfoNL, "ymax").InnerText);
                //框的設定
                Rectangle rect = new Rectangle();
                rect.X = xmin;
                rect.Y = ymin;
                rect.Width = Math.Abs(xmax - xmin);
                rect.Height = Math.Abs(ymax - ymin);

                list_recInfo.Add(new rec_info(ng, rect));
            }

            return list_recInfo;
        }


        private void drawRec(Image img, List<rec_info> list_recInfo)
        {
            //判斷img格式, indexPixel使用Graphics.FromImage(img) 會出error
            if (IsIndexedPixelFormat(img.PixelFormat))
            {
                Bitmap bmp = new Bitmap(img, img.Width, img.Height);
                img = bmp;
            }

            foreach (rec_info item in list_recInfo)
            {
                Rectangle rect = item.rect;
                string label = item.label;

                using (Graphics g = Graphics.FromImage(img))
                {
                    // Create pen.
                    Pen blackPen = new Pen(Color.Red, Math.Min(img.Size.Height, img.Size.Width) / 150);
                    // Create rectangle.
                    g.DrawRectangle(blackPen, rect);

                    if (hasText)
                    {
                        double wfactor = (double)pictureBox1.ClientSize.Width / img.Width;
                        double hfactor = (double)pictureBox1.ClientSize.Height / img.Height;
                        double resizeFactor = Math.Min(wfactor, hfactor);

                        Font drawFont = new Font("Arial", Convert.ToInt32(20 / resizeFactor));
                        SolidBrush drawBrush = new SolidBrush(Color.Blue);
                        PointF p = new PointF(rect.X, rect.Y);
                        g.DrawString(label, drawFont, drawBrush, p);
                    }
                }
            }
            pictureBox1.Image = img;
        }

        private bool IsIndexedPixelFormat(PixelFormat imagePixelFormat)
        {
            PixelFormat[] pixelFormatArray = {
                                            PixelFormat.Format1bppIndexed
                                            ,PixelFormat.Format4bppIndexed
                                            ,PixelFormat.Format8bppIndexed
                                            ,PixelFormat.Undefined
                                            ,PixelFormat.DontCare
                                            ,PixelFormat.Format16bppArgb1555
                                            ,PixelFormat.Format16bppGrayScale
                                        };
            foreach (PixelFormat pf in pixelFormatArray)
            {
                if (imagePixelFormat == pf)
                {
                    return true;
                }
            }
            return false;
        }

        public static Image resizeImage(Image imgToResize, Size size)
        {
            return (Image)(new Bitmap(imgToResize, size));
        }

        //取得XmlNodeList對應的子XmlNode
        private XmlNode getXmlNode(XmlNodeList NL, string nodeName)
        {
            foreach (XmlNode node in NL)
            {
                if (node.Name == nodeName)
                {
                    return node;
                }
            }
            return null;
        }

        private void bt_last_Click(object sender, EventArgs e)
        {
            if (images_Path == null)
            {
                MessageBox.Show("尚未選擇路徑");
                return;
            }

            if (cb_Filter.Text != "ALL")
            {
                int filterIndex = getFilterIndex(current_image_num, false);
                if (current_image_num == filterIndex)
                {
                    MessageBox.Show("無其他此Label");
                    return;
                }
                current_image_num = filterIndex;
            }
            else
            {
                current_image_num -= 1;
                if (current_image_num < 0)
                {
                    current_image_num = images_Path.Count() - 1;
                }
            }
            
            showImage(images_Path[current_image_num]);
            lb_dsc.Text = String.Format("第{0}張,共{1}張", current_image_num + 1, images_Path.Count());
        }

        private void bt_next_Click(object sender, EventArgs e)
        {
            if (images_Path == null)
            {
                MessageBox.Show("尚未選擇路徑");
                return;
            }

            if (cb_Filter.Text != "ALL")
            {
                int filterIndex = getFilterIndex(current_image_num, true);
                if (current_image_num == filterIndex)
                {
                    MessageBox.Show("無其他此Label");
                    return;
                }
                current_image_num = filterIndex;
            }
            else
            {
                current_image_num += 1;
                if (current_image_num >= images_Path.Count())
                {
                    current_image_num = 0;
                }
            }

            showImage(images_Path[current_image_num]);
            lb_dsc.Text = String.Format("第{0}張,共{1}張", current_image_num + 1, images_Path.Count());
        }

        private int getFilterIndex(int startIndex, bool order)
        {
            if (order)
            {
                for (int i = startIndex + 1; i < images_Label.Count(); i++)
                {
                    if (images_Label[i].Contains(cb_Filter.Text))
                    {
                        return i;
                    }
                }

                for (int i = 0; i < startIndex; i++)
                {
                    if (images_Label[i].Contains(cb_Filter.Text))
                    {
                        return i;
                    }
                }
            }
            else
            {
                for (int i = startIndex - 1; i > -1; i--)
                {
                    if (images_Label[i].Contains(cb_Filter.Text))
                    {
                        return i;
                    }
                }

                for (int i = images_Label.Count() - 1; i > startIndex; i--)
                {
                    if (images_Label[i].Contains(cb_Filter.Text))
                    {
                        return i;
                    }
                }
            }

            return startIndex;
        }

        private void panel2_SizeChanged(object sender, EventArgs e)
        {
            this.pictureBox1.Size = this.panel2.Size;
        }

        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            pictureBox1.Focus();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            if (e.Button == MouseButtons.Left)
            {
                //記錄摁下點座標，作為平移原點
                MouseDownPoint.X = PointToClient(System.Windows.Forms.Cursor.Position).X;
                MouseDownPoint.Y = PointToClient(System.Windows.Forms.Cursor.Position).Y;
                IsSelected = true;
                pictureBox1.Cursor = Cursors.Hand;
            }
        }

        //pboImage滑鼠滾輪事件
        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            //計算縮放後的錨點和寬高
            int i = e.Delta * SystemInformation.MouseWheelScrollLines / 4;
            int left = pictureBox1.Left - i / 2, top = pictureBox1.Top - i / 2;
            int width = pictureBox1.Width + i, heigth = pictureBox1.Height + i;

            if (i < 0)//縮小時需要考慮與顯示範圍間關係，放大時無需考慮
            {
                //計算縮放後圖片有效範圍
                double WidthScale = Convert.ToDouble(pictureBox1.Image.Width) / width;
                double HeigthScale = Convert.ToDouble(pictureBox1.Image.Height) / heigth;
                if (WidthScale > HeigthScale)
                {
                    top = top + Convert.ToInt32(Math.Ceiling(heigth - (pictureBox1.Image.Height / WidthScale))) / 2;
                    heigth = Convert.ToInt32(Math.Ceiling(pictureBox1.Image.Height / WidthScale));
                }
                else
                {
                    left = left + Convert.ToInt32(Math.Ceiling(width - (pictureBox1.Image.Width / HeigthScale))) / 2;
                    width = Convert.ToInt32(Math.Ceiling(pictureBox1.Image.Width / HeigthScale));
                }

                if (left > 0)//左側在顯示範圍內部，調整到左邊界
                {
                    if (width - left < panel2.Width) width = panel2.Width;
                    else width = width - left;
                    left = 0;
                }
                if (left + width < panel2.Width)//右側在顯示範圍內部，調整到右邊界
                {
                    if (panel2.Width - width > 0) left = 0;
                    else left = panel2.Width - width;
                    width = panel2.Width - left;
                }

                if (top > 0)//上側在顯示範圍內部，調整到上邊界
                {
                    if (heigth - top < panel2.Height) heigth = panel2.Height;
                    else heigth = heigth - top;
                    top = 0;
                }
                if (top + heigth < panel2.Height)//下側在顯示範圍內部，調整到下邊界
                {
                    if (panel2.Height - heigth > 0) top = 0;
                    else top = panel2.Height - heigth;
                    heigth = panel2.Height - top;
                }
            }

            pictureBox1.Width = width;
            pictureBox1.Height = heigth;
            pictureBox1.Left = left;
            pictureBox1.Top = top;
        }


        //pboImage滑鼠移動事件
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            //計算圖片有效範圍
            double WidthScale = Convert.ToDouble(pictureBox1.Image.Width) / pictureBox1.Width;
            double HeigthScale = Convert.ToDouble(pictureBox1.Image.Height) / pictureBox1.Height;
            int InvalidTop = pictureBox1.Top, InvalidHeigth = pictureBox1.Height, InvalidLeft = pictureBox1.Left, InvalidWidth = pictureBox1.Width;
            if (WidthScale > HeigthScale)
            {
                InvalidTop = InvalidTop + ((int)Math.Ceiling(InvalidHeigth - (pictureBox1.Image.Height / WidthScale))) / 2;
                InvalidHeigth = (int)Math.Ceiling(pictureBox1.Image.Height / WidthScale);
            }
            else
            {
                InvalidLeft = InvalidLeft + ((int)Math.Ceiling(InvalidWidth - (pictureBox1.Image.Width / HeigthScale))) / 2;
                InvalidWidth = (int)Math.Ceiling(pictureBox1.Image.Width / HeigthScale);
            }

            //滑鼠是否摁在圖片上
            bool IsMouseInPanel = InvalidLeft < PointToClient(System.Windows.Forms.Cursor.Position).X &&
            PointToClient(System.Windows.Forms.Cursor.Position).X < InvalidLeft + InvalidWidth &&
            InvalidTop < PointToClient(System.Windows.Forms.Cursor.Position).Y &&
            PointToClient(System.Windows.Forms.Cursor.Position).Y < InvalidTop + InvalidHeigth;
            if (IsSelected && IsMouseInPanel)
            {
                //計算平移後圖片有效範圍的錨點和寬高
                int left = InvalidLeft + (PointToClient(System.Windows.Forms.Cursor.Position).X - MouseDownPoint.X);
                int top = InvalidTop + (PointToClient(System.Windows.Forms.Cursor.Position).Y - MouseDownPoint.Y);
                int right = left + InvalidWidth;
                int down = top + InvalidHeigth;

                if (left >= InvalidLeft && left >= 0) left = 0; //向右平移且平移後在顯示範圍內部，調整到左邊界
                if (left < InvalidLeft && right <= panel2.Width) left = left + panel2.Width - right;//向左平移且平移後在顯示範圍內部，調整到右邊界
                if (top >= InvalidTop && top >= 0) top = 0;//向下平移且平移後在顯示範圍內部，調整到上邊界
                if (top < InvalidTop && down <= panel2.Height) top = top + panel2.Height - down;//向上平移且平移後在顯示範圍內部，調整到下邊界

                //有效範圍錨點換算到整體的錨點
                left = left + pictureBox1.Left - InvalidLeft;
                top = top + pictureBox1.Top - InvalidTop;

                if (InvalidLeft <= 0) pictureBox1.Left = left;
                if (InvalidTop <= 0) pictureBox1.Top = top;

                //記錄摁下點座標，作為平移原點
                MouseDownPoint.X = PointToClient(System.Windows.Forms.Cursor.Position).X;
                MouseDownPoint.Y = PointToClient(System.Windows.Forms.Cursor.Position).Y;
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;
            IsSelected = false;
            pictureBox1.Cursor = Cursors.SizeAll;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //>0時 選取當前的label
            //=0時 選取所有label
            if (listBox1.SelectedIndex > 0)
            {
                string image_path = images_Path[current_image_num];
                Image img = Image.FromFile(image_path);
                rec_info rInfo = list_rec_info[listBox1.SelectedIndex - 1];
                List<rec_info> list_recInfo = new List<rec_info>();
                list_recInfo.Add(rInfo);
                drawRec(img, list_recInfo);
            }
            else
            {
                string image_path = images_Path[current_image_num];
                Image img = Image.FromFile(image_path);
                drawRec(img, list_rec_info);
            }
            hasText = true;
        }

        //點擊兩下listbox,不顯示label文字只顯示框
        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            hasText = false;
            listBox1_SelectedIndexChanged(null, null);
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.Enter)
            {
                if (images_Path == null)
                {
                    MessageBox.Show("尚未選擇路徑");
                    return;
                }

                int num = 0;
                try
                {
                    num = Convert.ToInt32(textBox1.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show("請輸入數字");
                    textBox1.Text = "";
                    return;
                }

                if (num > images_Path.Count() || num < 1)
                {
                    MessageBox.Show("請確認範圍");
                    return;
                }

                current_image_num = num - 1;
                showImage(images_Path[current_image_num]);
                lb_dsc.Text = String.Format("第{0}張,共{1}張", current_image_num + 1, images_Path.Count());
            }
        }
    }
}
