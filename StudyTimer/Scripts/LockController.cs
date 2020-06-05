using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyTimer.Scripts
{
    sealed class LockController
    {
        private static LockController instance = null;
        private static readonly object padlock = new object();
        const char lockSep = '@'; 

        static Dictionary<string, List<StudyLock>> categorizedLocks = new Dictionary<string, List<StudyLock>>(); 

        LockController()
        { 
            if (Properties.Settings.Default.LockedStrings == null)
                Properties.Settings.Default.LockedStrings = new StringCollection();
            UnpackSaved();
        }

        public static LockController Instance {
            get { 
                lock (padlock)
                {
                    if (instance == null)
                        instance = new LockController(); 
                    return instance;
                } 
            }
        }

        void UnpackSaved()
        {
            foreach(string s in Properties.Settings.Default.LockedStrings)
            {
                int sepPos1 = s.IndexOf(lockSep);
                if (sepPos1 > 0)
                { 
                    string[] lockstrs = s.Split(lockSep);
                    if(lockstrs.Length >= 3)
                    {
                        StudyLock sl = new StudyLock(lockstrs[0], lockstrs[1], int.Parse(lockstrs[2]));
                        AddLock(sl, false); 
                    }
                }
                else { 
                    //ignore the disgusting ill-formatted heathen
                }
            }
            SaveLocks();
        }

        public List<StudyLock> GetLocks(string category)
        {
            if (!categorizedLocks.ContainsKey(category))
                return new List<StudyLock>();
            return categorizedLocks[category];
        }

        public void AddLock(StudyLock lok, bool save = true)
        {
            if (!categorizedLocks.ContainsKey(lok.Category)) 
                categorizedLocks.Add(lok.Category, new List<StudyLock>()); 
            categorizedLocks[lok.Category].Add(lok);
            if(save) SaveLocks();
        }

        public StudyLock GetLockAtIdx (string category, int idx)
        { 
            foreach(StudyLock s in categorizedLocks[category])
            {
                if (s.DesiredIdx == idx)
                    return s;
            }
            return null;
        }

        public void RemoveLock(string category, int idx, bool save = true)
        {
            if (!categorizedLocks.ContainsKey(category))
                categorizedLocks.Add(category, new List<StudyLock>());
            StudyLock sl = null;
            foreach(StudyLock s in categorizedLocks[category]) 
                if(s.DesiredIdx == idx)
                {
                    sl = s;
                    break;
                }    
            if(sl != null) categorizedLocks[category].Remove(sl);
            if (save) SaveLocks();
        }

        public bool EditLock(string category, string oldLockStr, string newLockStr, bool save = true)
        {
            //make sure category and old lock string exist in current lockset
            if (!categorizedLocks.ContainsKey(category)) return false;
            int lockIdx = LockStrIdx(categorizedLocks[category], oldLockStr); 
            if (lockIdx < 0) return false;
            categorizedLocks[category][lockIdx].LockStr = newLockStr;
            if (save) SaveLocks();
            return true;
        }

        int LockStrIdx(List<StudyLock> lks, string s)
        {
            for (int i = 0; i < lks.Count; i++)
            {
                StudyLock sl = lks[i];
                if (sl.LockStr == s)
                    return i;
            }
            return -1;
        }

        public bool OverwriteCategoryLocks(string category, List<StudyLock> newLocks, bool save = true) 
        { 
            if (!categorizedLocks.ContainsKey(category)) return false;
            categorizedLocks[category] = newLocks;
            if (save) SaveLocks();
            return true;
        }

        public void SaveLocks()
        {
            //pack dictionary into a single stringcollection
            StringCollection sc = new StringCollection();
            foreach (KeyValuePair<string, List<StudyLock>> entry in categorizedLocks)
            { 
                string categ = entry.Key;
                foreach(StudyLock s in entry.Value)
                {
                    string savedStr = categ + lockSep + s.LockStr + lockSep + s.DesiredIdx;
                    sc.Add(savedStr);
                }
            }
            Properties.Settings.Default.LockedStrings.Clear();
            Properties.Settings.Default.LockedStrings = sc;
            Properties.Settings.Default.Save();
        }
    }
}

class StudyLock
{
    string category;
    string lockStr;
    int desiredIdx; 
    public StudyLock(string category, string lockStr, int desiredIdx)
    {
        this.category = category;
        this.lockStr = lockStr;
        this.desiredIdx = desiredIdx;
    } 
    public string LockStr { get => lockStr; set => lockStr = value; }
    public int DesiredIdx { get => desiredIdx; set => desiredIdx = value; }
    public string Category { get => category; set => category = value; }
}
