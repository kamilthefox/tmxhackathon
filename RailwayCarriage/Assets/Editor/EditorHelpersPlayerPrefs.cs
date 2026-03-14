using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assets
{
    internal class EditorHelpersPlayerPrefs
    {
        [MenuItem("Tools/Clear FirstLaunchKey")]
        public static void ClearFirstLaunchKey()
        {
            PlayerPrefs.DeleteKey("FirstLaunchCompleted");
            PlayerPrefs.Save();
            Debug.Log("FirstLaunchKey удален из PlayerPrefs");
        }
    }
}
