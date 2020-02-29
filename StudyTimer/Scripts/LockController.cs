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
        const string lockSep = "*^*";

        static Dictionary<string, List<string>> categorizedLocks = new Dictionary<string, List<string>>();
        bool loaded = false;

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
                int sepPos = s.IndexOf(lockSep);
                if (sepPos > 0)
                {
                    string category = s.Substring(0, sepPos);
                    string lockStr = s.Substring(sepPos + lockSep.Length);
                    AddLock(category, lockStr, false); //can't save because we are enumerating through lockedStrings - double enumeration is illegal
                }
                else { 
                    //ignore the disgusting ill-formatted heathen
                }
            }
            SaveLocks();
        }

        public List<string> GetLocks(string category)
        {
            if (!categorizedLocks.ContainsKey(category))
                return new List<string>();
            return categorizedLocks[category];
        }

        public void AddLock(string category, string lockStr, bool save = true)
        {
            if (!categorizedLocks.ContainsKey(category)) 
                categorizedLocks.Add(category, new List<string>()); 
            categorizedLocks[category].Add(lockStr);
            if(save) SaveLocks();
        }

        public void RemoveLock(string category, string lockStr, bool save = true)
        {
            if (!categorizedLocks.ContainsKey(category))
                categorizedLocks.Add(category, new List<string>());
            categorizedLocks[category].Remove(lockStr);
            if (save) SaveLocks();
        }

        public bool EditLock(string category, string oldLockStr, string newLockStr, bool save = true)
        {
            //make sure category and old lock string exist in current lockset
            if (!categorizedLocks.ContainsKey(category)) return false; 
            if (!categorizedLocks[category].Contains(oldLockStr)) return false;
            categorizedLocks[category][categorizedLocks[category].IndexOf(oldLockStr)] = newLockStr;
            if (save) SaveLocks();
            return true;
        }

        public void SaveLocks()
        {
            //pack dictionary into a single stringcollection
            StringCollection sc = new StringCollection();
            foreach (KeyValuePair<string, List<string>> entry in categorizedLocks)
            { 
                string categ = entry.Key;
                foreach(string s in entry.Value)
                {
                    string savedStr = categ + lockSep + s;
                    sc.Add(savedStr);
                }
            }
            Properties.Settings.Default.LockedStrings.Clear();
            Properties.Settings.Default.LockedStrings = sc;
        }
    }
}
