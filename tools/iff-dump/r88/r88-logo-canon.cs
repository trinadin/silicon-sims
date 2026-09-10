// R88 IFF-LITERAL canon: the ORIGINAL 'The Sims' logo stamps - kNghSimsLogoEn/SP/FR/GR/JP +
// kDowntownCredits + kStudiotownCredits (7 members), byte-verbatim.
// (raw FAR stored name, byte length, sha256, BMP/TGA dims) - IFF-literal mount keys.
using System;

public class R88ResourceCanon
{
    public string Name; public int Bytes; public string Sha256; public int W; public int H;
    public R88ResourceCanon(string name, int bytes, string sha256, int w, int h) { Name = name; Bytes = bytes; Sha256 = sha256; W = w; H = h; }
}

public static readonly R88ResourceCanon[] R88LogoCanon = new R88ResourceCanon[] {
            new R88ResourceCanon("NghUI\\TheSimsLogo.bmp", 2934, "ebae562f5550abe7b5767b408b2cca8c59884807bda1525d73173ff11ec6d7ba", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_sp.bmp", 2888, "7b34f7129b401544a1c05963595adbf6a792a54086342b26f2013ef24e735b88", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_fr.bmp", 2930, "3a642e8cc38f0e37036f00dcf66875b7d8b6d0a8926df5fd5ea9f0ca7c204ded", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_gr.bmp", 2940, "7d08dae35b5852e475bc08a3601250f1cae913dd079d239d58bba81c883d1222", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_jp.bmp", 3594, "b14c795aab984407e5ee9621eb583406402d5f6fa66696bff7db5ae61daef932", 99, 52),
            new R88ResourceCanon("Downtown\\TheSimsLogo.bmp", 2934, "ebae562f5550abe7b5767b408b2cca8c59884807bda1525d73173ff11ec6d7ba", 70, 52),
            new R88ResourceCanon("Studiotown\\TheSimsLogo.bmp", 2934, "ebae562f5550abe7b5767b408b2cca8c59884807bda1525d73173ff11ec6d7ba", 70, 52),
        };
