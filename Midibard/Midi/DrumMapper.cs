using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.Midi
{
    internal class DrumMapper
    {
        /// <summary>
        /// author: MoogleTroupe
        /// </summary>
        /// <param name="noteNum"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static int MapNote(int noteNum) =>
            noteNum switch
            {
                35 => 8,
                36 => 10,
                38 => 20,
                40 => 22,
                41 => 16,
                43 => 19,
                45 => 23,
                47 => 26,
                48 => 30,
                49 => 24,
                50 => 33,
                52 => 22,
                55 => 30,
                57 => 24,
                60 => 23,
                61 => 20,
                _ => noteNum
            };


        public static bool IsDrumNote(int noteNum) =>
            (new Dictionary<int, bool>
            {
                {35,true},
                {36,true},
                {38,true},
                {40,true},
                {41,true},
                {43,true},
                {45,true},
                {47,true},
                {48,true},
                {49,true},
                {50,true},
                {52,true},
                {55,true},
                {57,true},
                {60,true},
                {61,true}
            }).ContainsKey(noteNum);
    }
}
