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
using StudyTimer.Scripts;
using System.Threading; 

namespace StudyTimer {
    /// <summary>
    /// Interaction logic for SetupWindow.xaml
    /// </summary>
    public partial class SetupWindow : Window {
        Random rand = new Random(); 
        MainWindow timerWindow; 
        List<string> lstSelectedStrsUI = new List<string>(); //current strings in the selected box at the bottom
        readonly string fileStr = "study.txt";
        readonly string strCategoryHeader = "----";
        bool internetWillDisable = true;
        bool loaded = false; 

        public SetupWindow() {
            InitializeComponent(); 
        } 

        void RandomizeSelected()
        {
            List<ListBox> boxes = FindVisualChildren<ListBox>(tabs); //get all listboxes in tabs - probably only contains the open one
            lstSelectedStrsUI = new List<string>(); 

            //add all the locked strings first 
            foreach (string s in LockController.Instance.GetLocks(GetSelectedCategory()))
                lstSelectedStrsUI.Add(s);

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
            int numChoices = Math.Max(numChoicesRaw - LockController.Instance.GetLocks(GetSelectedCategory()).Count, 0); //subtract locked strings from remaining rands to get how many we still need to randomly come up with
            List<List<int>> selectedIdxs = new List<List<int>>();  //lists of selected indexes per sub-list
            for (int i = 0; i < randomGroupLists.Count; i++) selectedIdxs.Add(new List<int>()); //populate with empty lists

            //choose numChoices random things, one from each list before spamming the last one
            for (int j = 0; j < numChoices; j++)
            {
                int numList = Math.Min(j + LockController.Instance.GetLocks(GetSelectedCategory()).Count, randomGroupLists.Count-1); //the category sub-list we choose a random thing from
                int randomChoice;
                string strChoice; 
                do
                {
                    randomChoice = rand.Next(0, randomGroupLists[numList].Value.Count);
                    string strHeader = randomGroupLists[numList].Key.Trim().Length > 0 ? ("[" + randomGroupLists[numList].Key + "]") : string.Empty; //add the section name for this entry
                    strChoice = strHeader + " " + randomGroupLists[numList].Value[randomChoice].ToString();
                } while ((selectedIdxs[numList].Contains(randomChoice) || lstSelectedStrsUI.Contains(strChoice)) && selectedIdxs[numList].Count < randomGroupLists[numList].Value.Count); //keep looking while the chosen entry has the same index or name as one that came before, and if there are still possible un-added entries in the list
                selectedIdxs[numList].Add(randomChoice);
                lstSelectedStrsUI.Add(strChoice);
            } 

            lstbSelected.ItemsSource = lstSelectedStrsUI;
            RefreshListBoxSelected();
        }  

        /// <summary>
        /// Re-adds the selected strings to the final box
        /// </summary>
        void RefreshListBoxSelected() {
            List<string> strs = new List<string>(); 
            strs.AddRange(lstSelectedStrsUI);

            //add lock symbols to the locked strings
            for (int i = 0; i < strs.Count; i++)
            {
                string s = strs[i]; 
                if (!strs[i].StartsWith("🔒") && LockController.Instance.GetLocks(GetSelectedCategory()).Contains(s))
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
                string selectedText = lstb.SelectedValue.ToString(); 
                lstSelectedStrsUI.Add(selectedText);
                RefreshListBoxSelected(); 
            }
        }

        private void lstbSelected_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) { 
            if (lstbSelected.SelectedItem != null) {
                //dont delete locked strings 
                if (LockController.Instance.GetLocks(GetSelectedCategory()).Contains(lstSelectedStrsUI[lstbSelected.SelectedIndex]))
                    return;
                lstSelectedStrsUI.RemoveAt(lstbSelected.SelectedIndex);
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
                    if (lb.SelectedItem != null && lstbSelected.SelectedItem != null)
                    {
                        string selectedStr = lb.SelectedValue.ToString();
                        string curSelectedStr = lstSelectedStrsUI[lstbSelected.SelectedIndex];

                        //if selected thing is locked, update the lock text 
                        bool edited = false;
                        if (LockController.Instance.GetLocks(GetSelectedCategory()).Contains(curSelectedStr))
                            edited = LockController.Instance.EditLock(GetSelectedCategory(), curSelectedStr, selectedStr);

                        if(edited) //only edit ui if editing happened
                            lstSelectedStrsUI[lstbSelected.SelectedIndex] = selectedStr;
                    }
                }
            }
            RefreshListBoxSelected(); 
        } 

        string GetSelectedCategory()
        {
            return ((TabItem)tabs.SelectedItem).Header.ToString(); 
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
            timerWindow.SetData((int)((TimeSpan)timePickerStudy.Value).TotalSeconds, (int)((TimeSpan)timePickerBreak.Value).TotalSeconds, lstSelectedStrsUI, internetWillDisable);
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
            if(Properties.Settings.Default.LastTabIdx < tabs.Items.Count)
                tabs.SelectedIndex = Properties.Settings.Default.LastTabIdx; //open to last selected tab last session 
            DelayInitialRandomize();  //have to delay briefly so the tabs and listboxes can fill properly so we can access the strings in the listboxes 
            loaded = true; //always do last here
        }

        async Task InitialRandomizeDelay() {  await Task.Delay(50); } 
        private async void DelayInitialRandomize()
        {
            await InitialRandomizeDelay();
            RandomizeSelected();
        }

        async Task RandomizeDelayTabChange() { await Task.Delay(50); }
        private async void DelayRandomizeOnTabChange()
        {
            await RandomizeDelayTabChange();
            RandomizeSelected();
        }

        void LoadLockedOptions()
        { 
            foreach (string s in LockController.Instance.GetLocks(GetSelectedCategory()))
                lstSelectedStrsUI.Add(s);
        } 

        private void btnReloadFile_Click(object sender, RoutedEventArgs e)
        { 
            LoadTabsFromFile();
            //StudyBrowser browser = new StudyBrowser();
            //browser.Show();
        }

        private void btnReorderUp_Click(object sender, RoutedEventArgs e)
        {
            if (lstbSelected.SelectedItem != null)
            {
                int wantedIdx = Math.Max(lstbSelected.SelectedIndex - 1, 0);
                int oldIdx = lstbSelected.SelectedIndex;
                string oldSlotStr = lstSelectedStrsUI[wantedIdx];
                lstSelectedStrsUI[wantedIdx] = lstSelectedStrsUI[lstbSelected.SelectedIndex];
                lstSelectedStrsUI[oldIdx] = oldSlotStr; 
                RefreshListBoxSelected();
            }
        }

        private void btnReorderDown_Click(object sender, RoutedEventArgs e)
        {
            if (lstbSelected.SelectedItem != null)
            {
                int wantedIdx = Math.Min(lstbSelected.SelectedIndex + 1, lstbSelected.Items.Count-1);
                int oldIdx = lstbSelected.SelectedIndex;
                string oldSlotStr = lstSelectedStrsUI[wantedIdx];
                lstSelectedStrsUI[wantedIdx] = lstSelectedStrsUI[lstbSelected.SelectedIndex];
                lstSelectedStrsUI[oldIdx] = oldSlotStr;
                RefreshListBoxSelected();
            }
        } 

        private void selectedMenuRightClickLock_Click(object sender, RoutedEventArgs e)
        { 
            string category = ((TabItem)tabs.SelectedItem).Header.ToString();
            string selectedStr = lstbSelected.SelectedItem.ToString();
            LockController.Instance.AddLock(category, selectedStr); 

            RefreshListBoxSelected();
        }

        private void selectedMenuRightClickUnlock_Click(object sender, RoutedEventArgs e)
        { 
            string category = ((TabItem)tabs.SelectedItem).Header.ToString();
            string selectedStr2 = lstbSelected.SelectedItem.ToString();
            string selectedStr = selectedStr2.Substring(2, selectedStr2.Length - 2); //remove lock icon
            LockController.Instance.RemoveLock(category, selectedStr); 

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

        private void tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { 
            //check we clicked a tab
            if (e.Source is TabControl)
            {
                e.Handled = true;

                //save last clicked tab so when we reopen the program we open to that one 
                if (!loaded) return;
                int idx = tabs.SelectedIndex;
                Properties.Settings.Default.LastTabIdx = idx;
                Properties.Settings.Default.Save();
                //RandomizeSelected();
                DelayRandomizeOnTabChange(); //delay so the box can finish loading before we choose our random entries
            }
        }

        private void OnTabSelected(object sender, RoutedEventArgs e)
        {
            var tab = sender as TabItem;
            if (tab != null)
            {
                // this tab is selected!
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        { 
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
             
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
             
        }

        private void tabs_LayoutUpdated(object sender, EventArgs e)
        {
        }

        private void tabs_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!loaded) return;
            //RandomizeSelected();
        }
    }
}
