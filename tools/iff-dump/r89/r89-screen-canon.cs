// R89 IFF-LITERAL canon: the ORIGINAL neighborhood SCREEN members from Res_Nbhd.RT -
// backdrops (kNbhdBck/_UL, kDowntown/kStudiotown/kMagiclandBackground, kVacationIsland),
// vacation sub-layers (kNghVacationPort/Trees) and the nessie family kNess1-3 (11
// members), byte-verbatim. Rows reuse the R88ResourceCanon row shape.
using System;

public class R88ResourceCanon
{
    public string Name; public int Bytes; public string Sha256; public int W; public int H;
    public R88ResourceCanon(string name, int bytes, string sha256, int w, int h) { Name = name; Bytes = bytes; Sha256 = sha256; W = w; H = h; }
}

public static readonly R88ResourceCanon[] R89ScreenCanon = new R88ResourceCanon[] {
            new R88ResourceCanon("Community\\NScreen_unleashed.bmp", 476112, "73f524db67ff02311c7f087ca203bf15acb4e5de0b555364bfc1d3eb6e496cda", 800, 600),
            new R88ResourceCanon("Nbhd\\NScreen.BMP", 476066, "c31c17fbeafb939d1d1b47dfaf1e315ee1b4c742669a4206d4fdef59983b55d4", 800, 600),
            new R88ResourceCanon("Downtown\\DScreen.bmp", 448672, "3bef8cba39618d2a214eecbc855be6de37b62d871a2a87bfaad9207e8ccfe93d", 800, 600),
            new R88ResourceCanon("Studiotown\\DScreen.bmp", 475972, "54f8a9a7bf00383705ca4e732ead7984d8e996aa4682c5e47f7e2008f1caa068", 800, 600),
            new R88ResourceCanon("Magicland\\DScreen.bmp", 471664, "5ef0d4b7e33caf0c313687a5ab92c578a98795f0ea4f5a48baa343041c7304d6", 800, 600),
            new R88ResourceCanon("VIsland\\visland.bmp", 459006, "0834a65f2a118bb3ba490d4f9152d2a44979246f2c3c6e0df3a9a59d159815d1", 800, 600),
            new R88ResourceCanon("VIsland\\visland_port.bmp", 25826, "f0fbef638dbbdeef70438e3505448266a2371950c74338afa754d6355201025b", 800, 600),
            new R88ResourceCanon("VIsland\\visland_trees.bmp", 8516, "032f9a665df337fb0b212dedb761b85e18790f80d950d2650210055ea1ffa5f7", 800, 600),
            new R88ResourceCanon("Community\\ness01.bmp", 1628, "7ef6efed96993115b0a06839de7eb345eaf43159ea9485a7e419181e7b857c0e", 106, 75),
            new R88ResourceCanon("Community\\ness02.bmp", 1838, "5799734d2f9195cbea9fe1bdb342ceca2ae2b2af3f6e2728bb9852f846994ba4", 106, 75),
            new R88ResourceCanon("Community\\ness03.bmp", 2338, "c244ecbd8692a920aa48c68fdb6e6656f9b3702762c4976f1ddd52c060ecf65b", 106, 75),
        };
