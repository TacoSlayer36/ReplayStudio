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
        public static List<KeyCode> ControlKeys = new List<KeyCode> { KeyCode.LeftControl, KeyCode.RightControl };
        public static List<KeyCode> ShiftKeys = new List<KeyCode> { KeyCode.LeftShift, KeyCode.RightShift };
        public static List<KeyCode> AltKeys = new List<KeyCode> { KeyCode.LeftAlt, KeyCode.RightAlt };
        public static bool IsPressingAny(List<KeyCode> keyList)
        {
            //if (ignoreModified)
            //{
            //    if (IsPressingAny(ControlKeys, false)) return false;
            //    if (IsPressingAny(ShiftKeys, false)) return false;
            //    if (IsPressingAny(AltKeys, false)) return false;
            //}

            foreach (KeyCode keyCode in keyList)
            {
                if (Input.GetKey(keyCode))
                    return true;
            }
            return false;
        }

        public static float Binomial(int n, int i)
        {
            float ni;
            float a1 = Factorial[n];
            float a2 = Factorial[i];
            float a3 = Factorial[n - i];
            ni = a1 / (a2 * a3);
            return ni;
        }

        public static float Bernstein(int n, int i, float t)
        {
            float t_i = Mathf.Pow(t, i);
            float t_n_minus_i = Mathf.Pow((1 - t), (n - i));

            float basis = Binomial(n, i) * t_i * t_n_minus_i;
            return basis;
        }

        // a look up table for factorials. Capped to 16.
        public static float[] Factorial = new float[]
        {
        1.0f,
        1.0f,
        2.0f,
        6.0f,
        24.0f,
        120.0f,
        720.0f,
        5040.0f,
        40320.0f,
        362880.0f,
        3628800.0f,
        39916800.0f,
        479001600.0f,
        6227020800.0f,
        87178291200.0f,
        1307674368000.0f,
        20922789888000.0f,
        };
    }
}
