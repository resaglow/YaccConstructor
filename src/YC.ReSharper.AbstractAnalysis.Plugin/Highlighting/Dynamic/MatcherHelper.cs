﻿using System.Collections.Generic;
using System.Linq;
using Highlighting.Core;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.UI.Resources;

namespace YC.ReSharper.AbstractAnalysis.Plugin.Highlighting.Dynamic
{
    public static class MatcherHelper
    {
        public static Core.Processor YcProcessor { get; set; }
        public static string AllMatchingValues = string.Empty;
        //Needs for dynamic highlighting
        public static List<ITreeNode> NodeCover { get; private set; }

        private static Dictionary<string, Dictionary<string, string>> allMatch;
        private static Dictionary<string, string> myMatched;

        static MatcherHelper()
        {
            NodeCover = new List<ITreeNode>();
            allMatch = new Dictionary<string, Dictionary<string, string>>();
        }
        
        public static void AddMatch(string ycLeft, string ycRight, string lang)
        {
            myMatched.Add(ycLeft, ycRight);
        }

        public static void UpdateMatchingValues(string lang)
        {
            foreach (KeyValuePair<string, string> pair in allMatch[lang])
            {
                var strLeft = YcHelper.GetStringValue(pair.Key, lang);
                if (!AllMatchingValues.Contains(strLeft)) 
                    AllMatchingValues += strLeft;

                var strRight = YcHelper.GetStringValue(pair.Value, lang);
                if (!AllMatchingValues.Contains(strRight)) 
                    AllMatchingValues += strRight;
            }
        }

        public static string GetRightMatch(string leftMatch, string lang)
        {
            lang = lang.ToLower();
            string ycToken = YcHelper.GetYcTokenName(leftMatch, lang);
            if (string.IsNullOrEmpty(ycToken))
                return null;

            string ycBrother = string.Empty;
            myMatched = allMatch[lang];
            if (myMatched.ContainsKey(ycToken))
            {
                ycBrother = myMatched[ycToken];
            }

            return YcHelper.GetStringValue(ycBrother, lang.ToLower());
        }

        public static string GetLeftMatch(string rightMatch, string lang)
        {
            lang = lang.ToLower();
            string ycToken = YcHelper.GetYcTokenName(rightMatch, lang);
            if (string.IsNullOrEmpty(ycToken))
                return null;

            string ycBrother = string.Empty;
            myMatched = allMatch[lang];
            if (myMatched.ContainsValue(ycToken))
            {
                ycBrother = myMatched.First(pairMatched => pairMatched.Value == ycToken).Key;
            }
            
            return YcHelper.GetStringValue(ycBrother, lang);
        }

        public static void ChangeLanguageTo(string lang)
        {
            if (!allMatch.ContainsKey(lang))
            {
                allMatch.Add(lang, new Dictionary<string, string>());
            }
            myMatched = allMatch[lang];
        }
    }
}
