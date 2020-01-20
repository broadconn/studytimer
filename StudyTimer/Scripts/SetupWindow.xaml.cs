using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfApp1;
using Xceed.Wpf.Toolkit;
using System.Windows.Controls.Primitives;
using System.IO;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace StudyTimer {
    /// <summary>
    /// Interaction logic for SetupWindow.xaml
    /// </summary>
    public partial class SetupWindow : Window {
        Random rand = new Random(); 
        MainWindow timerWindow; 
        List<string> lstSelectedStrs = new List<string>();
        readonly string fileStr = "study.txt";
        readonly string strCategoryHeader = "----";
        bool internetWillDisable = true; 

        public SetupWindow() {
            InitializeComponent();
            if(Properties.Settings.Default.LockedStrings == null)
                Properties.Settings.Default.LockedStrings = new StringCollection(); 
        } 

        void RandomizeSelected()
        {
            List<ListBox> boxes = FindVisualChildren<ListBox>(tabs); //get all listboxes in tabs - probably only contains the open one
            lstSelectedStrs = new List<string>(); 

            //add all the locked strings first
            foreach (string s in Properties.Settings.Default.LockedStrings)
                lstSelectedStrs.Add(s);

            //ensure we have a number of randoms to use
            if (txtbRandomOpts.Text.Length == 0)
                txtbRandomOpts.Text = "3";

            //put all of the open lists strings into a normal list
            List<string> lbStrs = new List<string>();
            foreach (ListBox lb in boxes) 
                for (int i = 0; i < lb.Items.Count; i++) 
                    if(lb.Items[i].ToString().Trim().Length > 0)
                        lbStrs.Add(lb.Items[i].ToString()); 

            //get lists of random groups - if no groups, on bigass list
            List<KeyValuePair<string, List<string>>> randomGroupLists = new List<KeyValuePair<string, List<string>>>(); 
            List<string> curList = new List<string>();
            string curHeader = ""; //name of sections, marked by "----"
            foreach (string s in lbStrs)
            {
                //if we reach a header string and we have saved items in our current list, add it and start a new list
                if (s.StartsWith(strCategoryHeader))
                {
                    if(curList.Count > 0) randomGroupLists.Add(new KeyValuePair<string, List<string>>(curHeader, curList));
                    curHeader = s.Replace("-", string.Empty).Trim();
                    curList = new List<string>(); 
                }
                else 
                    curList.Add(s); 
            } 
            randomGroupLists.Add(new KeyValuePair<string, List<string>>(curHeader, curList)); //add the remaining list to the collection

            //choose random from each option list in order, looping around if we need more
            int numChoicesRaw = int.Parse(txtbRandomOpts.Text);
            int numChoices = Math.Max(numChoicesRaw - Properties.Settings.Default.LockedStrings.Count, 0); //subtract locked strings from remaining rands to get 
            List<int> selectedIdxs = new List<int>();
            for (int j = 0; j < numChoices; j++)
            {
                int numList = (j + Properties.Settings.Default.LockedStrings.Count) % randomGroupLists.Count; //the list we choose a random thing from
                int randomChoice;
                string strChoice; 
                do
                {
                    randomChoice = rand.Next(0, randomGroupLists[numList].Value.Count);
                    string strHeader = randomGroupLists[numList].Key.Trim().Length > 0 ? ("[" + randomGroupLists[numList].Key + "]") : string.Empty; //add the section name for this entry
                    strChoice = strHeader + " " + filterString(randomGroupLists[numList].Value[randomChoice].ToString());
                } while (selectedIdxs.Contains(randomChoice) || lstSelectedStrs.Contains(strChoice));
                selectedIdxs.Add(randomChoice);
                lstSelectedStrs.Add( strChoice);
            } 

            lstbSelected.ItemsSource = lstSelectedStrs;
            RefreshListBoxSelected();
        } 

        //any post processing  
        string filterString(string s) { 
            return s;
        }

        /// <summary>
        /// Re-adds the selected strings to the final box
        /// </summary>
        void RefreshListBoxSelected() {
            List<string> strs = new List<string>(); 
            strs.AddRange(lstSelectedStrs);

            //add lock symbols to the locked strings
            for (int i = 0; i < strs.Count; i++)
            {
                string s = strs[i]; 
                if (!strs[i].StartsWith("🔒") && Properties.Settings.Default.LockedStrings.Contains(s))
                    strs[i] = "🔒" + s; 
            }

            lstbSelected.ItemsSource = strs;  
        }

        private void BtnRandomize_Click(object sender, RoutedEventArgs e) {
            RandomizeSelected();
        }

        //double clicked an option
        private void lstbAll_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            ListBox lstb = (ListBox)sender;
            if (lstb.SelectedItem != null) {
                string selectedText = filterString(lstb.SelectedValue.ToString()); 
                lstSelectedStrs.Add(selectedText);
                RefreshListBoxSelected(); 
            }
        }

        private void lstbSelected_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) { 
            if (lstbSelected.SelectedItem != null) {
                //dont delete locked strings
                if (Properties.Settings.Default.LockedStrings.Contains(lstSelectedStrs[lstbSelected.SelectedIndex]))
                    return;
                lstSelectedStrs.RemoveAt(lstbSelected.SelectedIndex);
                RefreshListBoxSelected();
            }
        }

        //right arrow button
        private void btnReplaceClick(object sender, RoutedEventArgs e) { 
            List<ListBox> boxes = FindVisualChildren<ListBox>(tabs); 
            if (tabs.SelectedItem != null)
            {
                TabItem openTab = ((TabItem)tabs.SelectedItem);
                foreach (ListBox lb in boxes)
                {
                    if(lb.SelectedItem != null && lstbSelected.SelectedItem != null)
                        lstSelectedStrs[lstbSelected.SelectedIndex] = filterString(lb.SelectedValue.ToString());
                }
            }
            RefreshListBoxSelected(); 
        } 

        private void Button_Click(object sender, RoutedEventArgs e) { 

        }   

        private void TimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {

        }

        private void CmbDividers_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }

        T FindFirstChild<T>(FrameworkElement element, string elName) where T : FrameworkElement {
            int childrenCount = VisualTreeHelper.GetChildrenCount(element);
            var children = new FrameworkElement[childrenCount];

            for (int i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
                children[i] = child;
                if (child is T && child.Name == elName)
                    return (T)child;
            }

            for (int i = 0; i < childrenCount; i++)
                if (children[i] != null) {
                    var subChild = FindFirstChild<T>(children[i], elName);
                    if (subChild != null)
                        return subChild;
                }

            return null;
        }

        public static List<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
            List<T> objs = new List<T>();
            if (depObj != null) {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) {
                        objs.Add((T)child);
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child)) {
                        objs.AddRange(FindVisualChildren<T>(child));
                    }
                }
            }
            return objs;
        }

        public enum SetupPanel {
            Warmup, Study, Original
        }

        private void btnGo_Click(object sender, RoutedEventArgs e) {
            timerWindow = new MainWindow();
            timerWindow.SetData((int)((TimeSpan)timePickerStudy.Value).TotalSeconds, (int)((TimeSpan)timePickerBreak.Value).TotalSeconds, lstSelectedStrs, internetWillDisable);
            timerWindow.Show();
            timerWindow.Run();
            Hide();
            timerWindow.OnStudyEnd += OnStudyEnd;
        }

        void OnStudyEnd() {
            timerWindow.OnStudyEnd -= OnStudyEnd;
            Show(); 
        }

        private void btnFile_Click(object sender, RoutedEventArgs e) { 
            if (!File.Exists(fileStr)) 
                CreateDefaultFile(); 
            Process.Start(fileStr);
        }

        void CreateDefaultFile() { 
            FileStream fs = File.Create(fileStr);
            string defaultData = "ExampleTab\nCreate tabs by\nclicking the\ndocument button\nbelow!\n\nYou\ncan\ndo it!"; //your data
            byte[] info = new UTF8Encoding(true).GetBytes(defaultData);
            fs.Write(info, 0, info.Length);
            fs.Close();
        }

        void LoadTabsFromFile() {
            //if no file, create one with default lines
            if (!File.Exists(fileStr)) {
                CreateDefaultFile();
            }

            //read file
            TabItem curTab = null;
            ListBox curLb = null;
            tabs.Items.Clear();
            string[] lines = File.ReadAllLines(fileStr);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                if (i == 0 || line.Length == 0) {
                    //create new tab
                    curTab = new TabItem();
                    curTab.FontFamily = (FontFamily)FindResource("Comfortaa");
                    curTab.Header = "";
                    tabs.Items.Add(curTab);
                }

                //new tab, this line is the header
                if (curTab.Header.ToString().Length == 0) {
                    curTab.Header = line;
                    //add the tab's listbox
                    curLb = new ListBox();
                    curLb.FontFamily = (FontFamily)FindResource("Comfortaa");
                    curLb.PreviewMouseDoubleClick += lstbAll_PreviewMouseDown;
                    curTab.Content = curLb;
                }
                else { //add this entry to the tab's listbox
                    curLb.Items.Add(line);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            LoadTabsFromFile();
            LoadLockedOptions();
            DelayInitialRandomize();  //have to delay briefly so the tabs and listboxes can fill properly so we can access the strings in the listboxes
        }

        async Task InitialRandomizeDelay() {  await Task.Delay(50); } 
        private async void DelayInitialRandomize()
        {
            await InitialRandomizeDelay();
            RandomizeSelected();
        }

        void LoadLockedOptions()
        {
            foreach (string s in Properties.Settings.Default.LockedStrings)
                lstSelectedStrs.Add(s);
        } 

        private void btnReloadFile_Click(object sender, RoutedEventArgs e)
        { 
            LoadTabsFromFile();
        }

        private void btnReorderUp_Click(object sender, RoutedEventArgs e)
        {
            if (lstbSelected.SelectedItem != null)
            {
                int wantedIdx = Math.Max(lstbSelected.SelectedIndex - 1, 0);
                int oldIdx = lstbSelected.SelectedIndex;
                string oldSlotStr = lstSelectedStrs[wantedIdx];
                lstSelectedStrs[wantedIdx] = lstSelectedStrs[lstbSelected.SelectedIndex];
                lstSelectedStrs[oldIdx] = oldSlotStr; 
                RefreshListBoxSelected();
            }
        }

        private void btnReorderDown_Click(object sender, RoutedEventArgs e)
        {
            if (lstbSelected.SelectedItem != null)
            {
                int wantedIdx = Math.Min(lstbSelected.SelectedIndex + 1, lstbSelected.Items.Count-1);
                int oldIdx = lstbSelected.SelectedIndex;
                string oldSlotStr = lstSelectedStrs[wantedIdx];
                lstSelectedStrs[wantedIdx] = lstSelectedStrs[lstbSelected.SelectedIndex];
                lstSelectedStrs[oldIdx] = oldSlotStr;
                RefreshListBoxSelected();
            }
        } 

        private void selectedMenuRightClickLock_Click(object sender, RoutedEventArgs e)
        {
            //cur properties 
            StringCollection curLockStrs = Properties.Settings.Default.LockedStrings; 
            if (curLockStrs == null) curLockStrs = new StringCollection();

            //get index of selected item in listbox  
            string selectedStr = lstbSelected.SelectedItem.ToString();  

            //set the new one
            if(!curLockStrs.Contains(selectedStr))
                curLockStrs.Add(selectedStr); 
             
            Properties.Settings.Default.LockedStrings = curLockStrs;
            Properties.Settings.Default.Save();

            //add lock icon  
            RefreshListBoxSelected();
        }

        private void selectedMenuRightClickUnlock_Click(object sender, RoutedEventArgs e)
        {
            //cur properties 
            StringCollection curLockStrs = Properties.Settings.Default.LockedStrings; 
            if (curLockStrs == null) curLockStrs = new StringCollection();
             
            string selectedStr2 = lstbSelected.SelectedItem.ToString();
            string selectedStr = selectedStr2.Substring(2, selectedStr2.Length - 2); //remove lock icon

            //see if the index is actually locked, remove it if so
            if (curLockStrs.Contains(selectedStr))
                curLockStrs.Remove(selectedStr); 
             
            Properties.Settings.Default.LockedStrings = curLockStrs;
            Properties.Settings.Default.Save(); 

            RefreshListBoxSelected();
        }  

        private void txtbRandomOpts_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtbRandomOpts.Text = Regex.Replace(txtbRandomOpts.Text, "[^0-9]+", "");
        }

        private void btnInternet_Click(object sender, RoutedEventArgs e)
        {
            //toggle internet
            internetWillDisable = !internetWillDisable;
            btnInternet.Content = internetWillDisable ? "🌐" : "🌑";
            btnInternet.ToolTip = "Internet will" + (internetWillDisable ? "" : " not") + " be disabled";
        }
    }
}
