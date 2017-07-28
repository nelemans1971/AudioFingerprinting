using System;
using System.Collections.Generic;
using System.Text;

namespace CDR.Indexer
{
    static class CDRUtils
    {

        // "TITEL" kan de volgende bitcodes hebben:
        // 0x00000001    Anders
        // 0x00000002    LP
        // 0x00000004    CD
        // 0x00000008    DVD
        // 0x00000010    DIGITAAL
        // 0x00000020    DIGILEEN
        // 0x00010000    Pop catalogus
        // 0x00020000    Klassieke catalogus

        // MEDEWERKER kan de volgende bitcodes hebben:
        // 0x00000001    Anders
        // 0x00000002    LP
        // 0x00000004    CD
        // 0x00000008    DVD
        // 0x00000010    DIGITAAL
        // 0x00000020    DIGILEEN
        // De volgende codering is echt nodig omdat je met de opdeling
        // klassiek/populair en componist/uitvoerder meerdere mogelijkheden
        // krijgt die voor problemen kunnen zorgen als je ze
        // niet apart codeerd.
        // 0x00000100    Medewerker Populair
        // 0x00000200    Componist Populair (Gereserveerd voor toekomstig gebruik)
        // 0x00000400    Medewerker Klassiek
        // 0x00000800    Componist Klassiek

        // UNIFORMETITEL kan de volgende bitcodes hebben:
        // 0x00000001    Anders
        // 0x00000002    LP
        // 0x00000004    CD
        // 0x00000008    DVD
        // 0x00000010    DIGITAAL
        // 0x00000020    DIGILEEN
        // 0x00010000    Pop catalogus
        // 0x00020000    Klassieke catalogus
        

        public static string BitcodeMedium2String(int bitcode)
        {
            string result = string.Empty;

            result += ((bitcode & 0x01) != 0) ? "anders " : "";
            result += ((bitcode & 0x02) != 0) ? "lp " : "";
            result += ((bitcode & 0x04) != 0) ? "cd " : "";
            result += ((bitcode & 0x08) != 0) ? "dvd " : "";
            result += ((bitcode & 0x10) != 0) ? "digital " : "";
            result += ((bitcode & 0x20) != 0) ? "digileen " : "";

            return result;
        }

        public static int MediumStr2Bitcode(string medium)
        {
            int result = 0;

            medium = medium.ToLower();
            if (medium.Contains("anders"))
            {
                result |= 0x01;
            }
            if (medium.Contains("lp"))
            {
                result |= 0x02;
            }
            if (medium.Contains("cd"))
            {
                result |= 0x04;
            }
            if (medium.Contains("dvd"))
            {
                result |= 0x08;
            }
            if (medium.Contains("digital"))
            {
                result |= 0x10;
            }
            if (medium.Contains("digileen"))
            {
                result |= 0x20;
            }

            return result;
        }


        public static string BitcodeCatalogus2String(int bitcode)
        {
            string result = string.Empty;

            result += ((bitcode & (0x010000)) != 0) ? "popular " : "";
            result += ((bitcode & (0x020000)) != 0) ? "classical " : "";
            result += ((bitcode & (0x040000)) != 0) ? "classicalold " : "";

            return result.Trim();
        }


        public static int CatalogusString2Bitcode(string catalogus)
        {
            int result = 0;

            catalogus = catalogus.ToLower() + " ";
            if (catalogus.Contains("popular"))
            {
                result |= 0x010000;
            }
            if (catalogus.Contains("classical "))
            {
                result |= 0x020000;
            }
            if (catalogus.Contains("classicalold"))
            {
                result |= 0x040000;
            }

            return result;
        }

        public static string BitcodeEigenaar2String(int bitcode)
        {
            string result = string.Empty;

            result += ((bitcode & 0x0001) != 0) ? "CDR " : "";
            result += ((bitcode & 0x0002) != 0) ? "NIETCDR " : "";

            return result.Trim();
        }

        public static int EigenaarString2Bitcode(string eigenaar)
        {
            int result = 0;

            eigenaar = eigenaar.ToLower().Trim();
            if (eigenaar == "cdr")
            {
                result |= 0x000001;
            }
            else if (eigenaar.Length > 0)
            {
                result |= 0x000002;
            }

            return result;
        }

        public static string BitcodeMedewerker2String(int bitcode)
        {
            string result = string.Empty;

            result += ((bitcode & 0x0100) != 0) ? "performerpopular " : "";
            result += ((bitcode & 0x0200) != 0) ? "composerpopular " : "";
            result += ((bitcode & 0x0400) != 0) ? "performerclassical " : "";
            result += ((bitcode & 0x0800) != 0) ? "composerclassical " : "";

            return result.Trim();
        }


    }
}
