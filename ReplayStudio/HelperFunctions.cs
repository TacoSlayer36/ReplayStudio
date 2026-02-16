using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReplayStudio
{
    internal static class HelperFunctions
    {
        /// <summary>
        /// Checks if any key in a list of KeyCodes is being pressed
        /// </summary>
        /// <param name="keyList"> The list to check </param>
        /// <returns> True if any key in the list is pressed </returns>
        public static bool IsPressing(List<KeyCode> keyList)
        {
            foreach (KeyCode keyCode in keyList)
            {
                if (Input.GetKey(keyCode))
                    return true;
            }
            return false;
        }
    }
}
