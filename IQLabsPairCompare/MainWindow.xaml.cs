using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO; // for file IO
using System.Drawing;

namespace IQLabsPairCompare
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //private string[] filePairs;
        List<string> filePairs = new List<string>();
        private double currentZoom;
        private int currentImagePair;
        ScaleTransform xform1;
        ScaleTransform xform2;
        string startOfExperiment;
        bool leftRightSwap;
        Random random;
        bool mouseDragging;
        System.Windows.Point mouseDown;
        double orgScroll1X, orgScroll1Y, orgScroll2X, orgScroll2Y;

        ImageSource imageSource1;
        ImageSource imageSource2;

        // this function is called when the app is started
        public MainWindow()
        {
            InitializeComponent();

            // WPF uses transforms to scale images, we will scale left/right
            // image independently as they may not be same size
            TransformGroup xformgroup1 = new TransformGroup();
            TransformGroup xformgroup2 = new TransformGroup();

            xform1 = new ScaleTransform(); // need a scale transform for each image
            xform2 = new ScaleTransform();
            xformgroup1.Children.Add(xform1); // add the scale transform to the transform grous
            xformgroup2.Children.Add(xform2);

            image1.LayoutTransform = xformgroup1; // apply the transform group to the image
            image2.LayoutTransform = xformgroup2;

            random = new Random(); // create random number generator for left/right image swap

            currentZoom = 1; // 1 is not 1x, but scale to fit screen. 2x is 2x in H/V direction.
            mouseDragging = false; // not dragging (panning) anything
        }

        // all button handler functions and UI callbacks come first

        // 
        private void button_openFolder1_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (dialog.SelectedPath != "")
                textBox_folder1.Text = dialog.SelectedPath;
        }

        private void button_openFolder2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (dialog.SelectedPath != "")
                textBox_folder2.Text = dialog.SelectedPath;
        }

        private void button_preferLeft_Click(object sender, RoutedEventArgs e)
        {
            if (leftRightSwap == false)
                updatePreferenceLog(0); // write preference for Left image
            else
                updatePreferenceLog(2); // write preference for Right image, as image from folder one was shown on right (swapped)

            // if image is not last image, show the next image
            if (currentImagePair < filePairs.Count - 1)
            {
                currentImagePair++; // use next entry in image list
                showNextImagePair();
            }
            else
            { // otherwise let user know we are at the end of the test
                button_preferLeft.IsEnabled = false;
                button_dontcare.IsEnabled = false;
                button_preferRight.IsEnabled = false;
                MessageBox.Show("No more images to evaluate");
            }
        }

        private void button_preferRight_Click(object sender, RoutedEventArgs e)
        {
            if (leftRightSwap == false)
                updatePreferenceLog(2); // write preference for Right image
            else
                updatePreferenceLog(0); // write preference for Left image, as image from folder two was shown on Left (swapped)
            // if image is not last image, show the next image
            if (currentImagePair < filePairs.Count - 1)
            {
                currentImagePair++; // use next entry in image list
                showNextImagePair();
            }
            else
            { // otherwise let user know we are at the end of the test
                button_preferLeft.IsEnabled = false;
                button_dontcare.IsEnabled = false;
                button_preferRight.IsEnabled = false;
                MessageBox.Show("No more images to evaluate");
            }
        }

        private void button_dontcare_Click(object sender, RoutedEventArgs e)
        {
            updatePreferenceLog(1); // write no preference indicator
            // if image is not last image, show the next image
            if (currentImagePair < filePairs.Count - 1)
            {
                currentImagePair++; // use next entry in image list
                showNextImagePair();
            }
            else
            { // otherwise let user know we are at the end of the test
                button_preferLeft.IsEnabled = false;
                button_dontcare.IsEnabled = false;
                button_preferRight.IsEnabled = false;
                MessageBox.Show("No more images to evaluate");
            }
        }

        private void button_startTest_Click(object sender, RoutedEventArgs e)
        {
            if (createImagePairList() == true)
            { // collect the list of image pairs
                // check to make sure there are image pairs to show to the users
                if (filePairs.Count == 0)
                {
                    MessageBox.Show("There are no matching pairs of image filenames in folders 1 and 2");
                    return;
                }

                startOfExperiment = DateTime.Now.ToString(); // get date/time. this will be used in log file name
                currentImagePair = 0; // reset to first image pair in list
                showNextImagePair();
                button_preferLeft.IsEnabled = true;
                button_dontcare.IsEnabled = true;
                button_preferRight.IsEnabled = true;
            }
        }

        // capture key presses used as shortcuts for prefer left/right/etc
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // check that we're not in the text boxes, and there are images to evaluate
            if ((textBox_ObserverID.IsFocused == false) &&
                (textBox_folder1.IsFocused == false) &&
                (textBox_folder2.IsFocused == false) &&
                (filePairs.Count > 0))
            {
                if (e.Key == Key.Z) // Z is prefer left
                    updatePreferenceLog(0);
                else if (e.Key == Key.C) // C is prefer right
                    updatePreferenceLog(2);
                else if (e.Key == Key.X) // X is no preference
                    updatePreferenceLog(1);
                else if ((e.Key == Key.OemPlus) && (Keyboard.Modifiers == ModifierKeys.Control))
                {// zoom in 
                    if (currentZoom < 8)
                    { // allow zoom up to 8x
                        currentZoom *= 2;
                        resizeImages(1);
                    }
                    return;
                }
                else if ((e.Key == Key.OemMinus) && (Keyboard.Modifiers == ModifierKeys.Control))
                {// zoom out 
                    if (currentZoom > 1)
                    { // 
                        currentZoom /= 2;
                        resizeImages(-1);
                    }
                    return;
                }
                else
                    return;

                // show next image if it exists
                if (currentImagePair < filePairs.Count - 1)
                {
                    currentImagePair++;
                    showNextImagePair();
                }
                else // otherwise we're done
                    MessageBox.Show("No more images to evaluate");
            }
        }

        // Mouse wheel zoom
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true; // don't let event bubble to scroll viewer

            if (e.Delta > 1)
            { // zoom in
                if (currentZoom < 8)
                { // allow zoom up to 8x
                    currentZoom *= 2;
                    resizeImages(1);
                }
            }
            else if (e.Delta < 1)
            { // zoom out
                if (currentZoom > 1)
                { // 1x (fit) scaling is smallest scale
                    currentZoom /= 2;
                    resizeImages(-1);
                }
            }
        }

        // mouse down on image 1, initialize for pan
        private void image1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            mouseDragging = true;
            mouseDown = e.GetPosition(mainAppWindow); // get position, used to calculate the scrollviewer offset
            orgScroll1X = scrollViewer1.HorizontalOffset; // save location where mouse was pressed
            orgScroll1Y = scrollViewer1.VerticalOffset;

            if (checkBox_SyncPanZoom.IsChecked == true)
            { // if sync pan, then also save other image's offset
                orgScroll2X = scrollViewer2.HorizontalOffset;
                orgScroll2Y = scrollViewer2.VerticalOffset;
            }
            return;
        }

        // mouse down on image 1, initialize for pan
        private void image2_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            mouseDragging = true;
            mouseDown = e.GetPosition(mainAppWindow); // get position, used to calculate the scrollviewer offset
            orgScroll2X = scrollViewer2.HorizontalOffset; // save location where mouse was pressed
            orgScroll2Y = scrollViewer2.VerticalOffset;

            if (checkBox_SyncPanZoom.IsChecked == true)
            { // if sync pan, then also save other image's offset
                orgScroll1X = scrollViewer1.HorizontalOffset;
                orgScroll1Y = scrollViewer1.VerticalOffset;
            }
            return;
        }

        private void image1_MouseMove(object sender, MouseEventArgs e)
        {
            if ((mouseDragging == true) && (currentZoom > 1))
            { // only pan if mouse button down, and zoomed in
                System.Windows.Point here = e.GetPosition(mainAppWindow); // get current mouse position

                if (orgScroll1X + (mouseDown.X - here.X) > 0) // don't pan further than needed (H)
                    scrollViewer1.ScrollToHorizontalOffset(orgScroll1X + (mouseDown.X - here.X));
                if (orgScroll1Y + (mouseDown.Y - here.Y) > 0) // don't pan further than needed (V)
                    scrollViewer1.ScrollToVerticalOffset(orgScroll1Y + (mouseDown.Y - here.Y));

                if (checkBox_SyncPanZoom.IsChecked == true)
                { // pan other window too if needed
                    if (orgScroll2X + (mouseDown.X - here.X) > 0) // don't pan further than needed (H)
                        scrollViewer2.ScrollToHorizontalOffset(orgScroll2X + (mouseDown.X - here.X));
                    if (orgScroll2X + (mouseDown.Y - here.Y) > 0) // don't pan further than needed (V)
                        scrollViewer2.ScrollToVerticalOffset(orgScroll2Y + (mouseDown.Y - here.Y));
                }

                label1.Content = mouseDown.X + "," + here.X + "," + scrollViewer1.HorizontalOffset; // debug
            }
        }

        // similar to function above
        private void image2_MouseMove(object sender, MouseEventArgs e)
        {
            if ((mouseDragging == true) && (currentZoom > 1))
            { // only pan if mouse button down, and zoomed in
                System.Windows.Point here = e.GetPosition(mainAppWindow);

                if (orgScroll2X + (mouseDown.X - here.X) > 0)
                    scrollViewer2.ScrollToHorizontalOffset(orgScroll2X + (mouseDown.X - here.X));
                if (orgScroll2Y + (mouseDown.Y - here.Y) > 0)
                    scrollViewer2.ScrollToVerticalOffset(orgScroll2Y + (mouseDown.Y - here.Y));

                if (checkBox_SyncPanZoom.IsChecked == true)
                {
                    if (orgScroll1X + (mouseDown.X - here.X) > 0)
                        scrollViewer1.ScrollToHorizontalOffset(orgScroll1X + (mouseDown.X - here.X));
                    if (orgScroll1Y + (mouseDown.Y - here.Y) > 0)
                        scrollViewer1.ScrollToVerticalOffset(orgScroll1Y + (mouseDown.Y - here.Y));
                }
            }
        }

        // if mouse is outside app window, stop panning
        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            mouseDragging = false;
            return;
        }

        // if mouse button released, stop panning
        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            mouseDragging = false;
            return;
        }



        // helper functions

        // write image filename and user preference to log file
        private bool updatePreferenceLog(int score)
        {
            string logFilename = textBox_ObserverID.Text.ToString() + "_" + startOfExperiment + ".csv";
            string result = filePairs[currentImagePair] + ",";

            if (score == 0)
            {
                result += "1,0,";
            }
            else if (score == 1)
            {
                result += "0,0,";
            }
            else if (score == 2)
            {
                result += "0,1,";
            }

            if ((score == 0) && ((bool)(checkBox_disaster.IsChecked)))
            {
                result += "0,1";
            }
            else if ((score == 2) && ((bool)(checkBox_disaster.IsChecked)))
            {
                result += "1,0";
            }
            else
                result += "0,0";

            logFilename = logFilename.Replace(':', '_'); // the date/time string contains illegal characters.
            logFilename = logFilename.Replace('/', '_');

            bool firstWrite = false;
            if (File.Exists(logFilename) == false)
                firstWrite = true;

            try
            {
                using (StreamWriter writer = File.AppendText(logFilename))
                {
                    if (firstWrite == true)
                    {
                        writer.WriteLine("F1 = " + textBox_folder1.Text);
                        writer.WriteLine("F2 = " + textBox_folder2.Text);
                        writer.WriteLine("Observer = " + textBox_ObserverID.Text);
                        writer.WriteLine();
                        writer.WriteLine("Image,Pref.F1,Pref.F2,F1 Disaster,F2 Disaster");
                    }
                    writer.WriteLine(result);
                    writer.Close();
                }
            }
            catch
            {
                MessageBox.Show("could not write log file (" + logFilename + ")");
            }

            return true;
        }

        // show pair of images from the list of matching pairs, using the current entry in the list of pairs
        private bool showNextImagePair()
        {
            currentZoom = 1; // always start with images fir to screen
            leftRightSwap = (random.Next(0, 2) == 1);

            checkBox_disaster.IsChecked = false;

            // read images from file from list of pairs
            // set GUI image boxes with image from JPEG files
            if (leftRightSwap == false)
            { // normal assignment
                imageSource1 = new BitmapImage(new Uri(textBox_folder1.Text + "//" + filePairs[currentImagePair]));
                imageSource2 = new BitmapImage(new Uri(textBox_folder2.Text + "//" + filePairs[currentImagePair]));
            }
            else
            {
                imageSource1 = new BitmapImage(new Uri(textBox_folder2.Text + "//" + filePairs[currentImagePair]));
                imageSource2 = new BitmapImage(new Uri(textBox_folder1.Text + "//" + filePairs[currentImagePair]));
            }

            resizeImages(0); // scale to desired size

            // set GUI image boxes with image from JPEG files
            image1.Source = imageSource1;
            image2.Source = imageSource2;

            // update the pair counter
            label_pairindex.Content = (currentImagePair + 1).ToString() + "/" + filePairs.Count;

            return true;
        }

        // function to scale images according to user scaling ratio
        private bool resizeImages(int direction)
        {
            // get ratio of GUI window size to actual image size, this becomes the scale ratio
            double xscale1 = scrollViewer1.ViewportWidth / imageSource1.Width;
            double yscale1 = scrollViewer1.ViewportHeight / imageSource1.Height;
            // pick the smallest of these, as we want to scale x/y equally
            double scale1 = Math.Min(xscale1, yscale1) * currentZoom;

            // apply scale transform
            xform1.ScaleX = scale1;
            xform1.ScaleY = scale1;

            // repeat for right image
            double xscale2 = scrollViewer2.ViewportWidth / imageSource2.Width;
            double yscale2 = scrollViewer2.ViewportHeight / imageSource2.Height;
            double scale2 = Math.Min(xscale2, yscale2) * currentZoom;
            xform2.ScaleX = scale2;
            xform2.ScaleY = scale2;

            // move images so new center of the image is same as old center of the image
            if (direction == 1)
            {
                double xo = scrollViewer1.HorizontalOffset;

                if (currentZoom == 2)
                    xo = scrollViewer1.ViewportWidth / 2;
                else if (currentZoom == 4)
                    xo = xo + scrollViewer1.ViewportWidth;
                else if (currentZoom == 8)
                    xo = xo + scrollViewer1.ViewportWidth * 2;

                scrollViewer1.ScrollToHorizontalOffset(xo);

                double yo = scrollViewer1.VerticalOffset;
                if (currentZoom == 2)
                    yo = yo + scrollViewer1.ViewportWidth / 4;
                scrollViewer1.ScrollToVerticalOffset(xo);

                label1.Content = xo;
            }
            else if (direction == -1)
            {
                double xo = scrollViewer1.HorizontalOffset;

                if (currentZoom == 2)
                    xo = scrollViewer1.ViewportWidth / 2;
                else if (currentZoom == 4)
                    xo = -(xo + scrollViewer1.ViewportWidth);
                else if (currentZoom == 8)
                    xo = -(xo + scrollViewer1.ViewportWidth * 2);

                scrollViewer1.ScrollToHorizontalOffset(xo);

                double yo = scrollViewer1.VerticalOffset;
                if (currentZoom == 2)
                    yo = yo - scrollViewer1.ViewportWidth / 4;
                scrollViewer1.ScrollToVerticalOffset(xo);

                label1.Content = xo;
            }

            return true;
        }

        // function to read the two given folders to extract the files with matching file names in both folders
        private bool createImagePairList()
        {
            // check if the folders are valid folders
            if (Directory.Exists(textBox_folder1.Text) == false)
            {
                MessageBox.Show("Could not find " + textBox_folder1.Text);
                return false;
            }
            if (Directory.Exists(textBox_folder2.Text) == false)
            {
                MessageBox.Show("Could not find " + textBox_folder2.Text);
                return false;
            }

            // create arrays of all jpg files in folder 1 and 2. the strings include full path, need to strip that.
            string[] filesFolder1 = Directory.GetFiles(textBox_folder1.Text, "*.jpg");
            string[] filesFolder2 = Directory.GetFiles(textBox_folder2.Text, "*.jpg");

            filePairs.Clear(); // clear array of matching file pairs, we're about to populate it again

            // if a jpg file exists in both folders, add to image pair array
            for (int i = 0; i < filesFolder1.Length; i++)
            {
                for (int j = 0; j < filesFolder1.Length; j++)
                {
                    // if file name (not including full path) matches in both folders, then add to list.
                    if (System.IO.Path.GetFileName(filesFolder1[i]) == System.IO.Path.GetFileName(filesFolder2[j]))
                        filePairs.Add(System.IO.Path.GetFileName(filesFolder1[i]));
                }
            }

            // update label to show total number of pairs of images
            label_pairindex.Content = "0/" + filePairs.Count;

            return true;
        }
    }
}
