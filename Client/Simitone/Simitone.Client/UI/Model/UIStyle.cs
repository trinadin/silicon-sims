using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Model
{
    public class UIStyle
    {
        public static UIStyle DARK = new UIStyle();
        
        public static UIStyle Current = DARK;


        //class definition

        // UI-parity (Round 67): palette aligned to the ORIGINAL game's UI sprites (extracted
        // byte-faithfully from UIGraphics.far, tools/iff-dump/uigr-orig/ - PARITY 'uipal').
        // PanelBack.bmp / CreateACharBack.bmp / PersBkg.bmp are all dark NAVY (#000029..#00106B,
        // anchors #000029 #000052 #00004A #080852 with steel-blue #73739C highlights); the original
        // accent color is CYAN #00FFFF (Mood.bmp / JobFriendSmiley.bmp). Not pure black, not green.
        public Color Bg = new Color(0, 0, 41) * 0.75f;          // #000029 (original panel navy)
        public Color TitleBg = new Color(0, 0, 24) * 0.85f;     // darker navy for title bars
        public Color SecondaryText = new Color(0, 255, 255);    // original cyan accent #00FFFF

        public Color BtnNormal = Color.White;
        public Color BtnActive = new Color(0, 255, 128, 255);
        public Color BtnDisable = new Color(128, 128, 128, 255);

        public Color ActiveSelection = Color.Yellow;

        public Color Text = Color.White;

        public Color DialogBg = Color.Black * 0.8f;
        public Color DialogText = Color.White;
        public Color DialogTitle = Color.Black;

        public Color BtnTxt = new Color(0, 31, 63);
        public Color GreenBtnTxt = new Color(0, 63, 16);
        public Color BtnTxtShadow = Color.White * 0.5f;

        // DISCLOSED port colors: the ±money floater is a port addition (the
        // original corpus has no matching floater composition — see the
        // UIMoneyPanel disclosure), so these hues are port-authored, not
        // decoded.
        public Color PosMoney = new Color(0, 255, 128, 255);
        public Color NegMoney = new Color(255, 128, 0, 255);

        public Color SkillInactive = new Color(99, 109, 242, 255);
        public Color SkillActive = new Color(0, 255, 255, 255);
        public Color SkillNeeded = new Color(255, 191, 0, 255);

        public Color TransColor = new Color(0, 41, 69, 255);
    }
}
