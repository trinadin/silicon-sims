/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System.Collections.Generic;

namespace Simitone.Client
{
    // ROUND-46 'catalog' IFF-LITERAL canon: the ORIGINAL buy-mode catalog surface, decoded
    // engine-independently from raw OBJD chunks (tools/iff_objd_canon.py + iff_objd_raw.py over
    // game-data/The Sims). The engine's TS1ObjectProvider builds the served catalog IFF-literally
    // from these same original OBJD tables (Price = obj.Price, Category = log2(FunctionFlags) /
    // BuildModeType+7, passesCatalogCheck predicate). IFF-literally pins PRICE / CATEGORY / entry
    // ELIGIBILITY: any engine-side hardcode, drift or dropped/phantom entry FAILS the 'catalog' check.
    public static class AutotestCatalogCanon
    {
        // ROUND-67 'uipal' IFF-LITERAL canon: ORIGINAL UI palette anchors, read byte-faithfully
        // from UIGraphics.far (tools/extract_uigr.py + tools/bmp_palette.py -> tools/iff-dump/uigr-orig/):
        //   PanelBack.bmp(804x100)  #000029 #000052 #00004A #080852 + steel #73739C #636394
        //   CreateACharBack.BMP(800x600) #00186B #00106B #000029 (CAS backdrop)
        //   PersBkg.bmp #3C3C7A #0E0E5B
        //   Greenbars/Redbars backing #72729E #7474A0 ; Mood.bmp cyan accent #00FFFF
        // The engine client's UIStyle is aligned to these (navy panels, cyan accent) - see CheckUIPalette.
        public static readonly uint[] OriginalUIPalette = new uint[] {
            0x000029, 0x000052, 0x00004A, 0x080852, /* PanelBack anchors */
            0x73739C, 0x636394, 0x5A5A8C,           /* steel-blue highlights   */
            0x00186B, 0x00106B, 0x001063,           /* CreateACharBack         */
            0x72729E, 0x7474A0,                     /* motive-bar backing      */
            0x00FFFF                                /* cyan accent (Mood)      */
        };
        public static readonly Dictionary<uint, int[]> CatalogCanon = new Dictionary<uint, int[]>{
            { 0x000001ca, new int[] { 200, 8 } },
            { 0x000c147f, new int[] { 875, 6 } },
            { 0x00230edc, new int[] { 180, 6 } },
            { 0x00c2b68f, new int[] { 675, 0 } },
            { 0x00c8a5f1, new int[] { 940, 10 } },
            { 0x00dd7908, new int[] { 289, 11 } },
            { 0x00ddfef9, new int[] { 375, 4 } },
            { 0x00e22e1e, new int[] { 110, 7 } },
            { 0x00e30dc2, new int[] { 185, 9 } },
            { 0x00fe9850, new int[] { 5888, 6 } },
            { 0x01259297, new int[] { 40, 1 } },
            { 0x0125da9d, new int[] { 170, 1 } },
            { 0x0133d442, new int[] { 1200, 1 } },
            { 0x01f02564, new int[] { 9000, 5 } },
            { 0x029c9043, new int[] { 790, 5 } },
            { 0x02f04067, new int[] { 300, 1 } },
            { 0x031db391, new int[] { 1559, 10 } },
            { 0x048567b7, new int[] { 50, 1 } },
            { 0x04d86d1f, new int[] { 0, 6 } },
            { 0x05777d82, new int[] { 749, 6 } },
            { 0x05d17306, new int[] { 260, 13 } },
            { 0x05dac986, new int[] { 0, 6 } },
            { 0x068ead71, new int[] { 150, 2 } },
            { 0x0768a9cb, new int[] { 993, 0 } },
            { 0x09196e73, new int[] { 89, 9 } },
            { 0x0924bc1f, new int[] { 109, 0 } },
            { 0x099a621e, new int[] { 582, 1 } },
            { 0x09cc9d1c, new int[] { 90, 13 } },
            { 0x09d8dcec, new int[] { 250, 11 } },
            { 0x0a02f059, new int[] { 45, 1 } },
            { 0x0a717442, new int[] { 89, 13 } },
            { 0x0a8178e9, new int[] { 229, 4 } },
            { 0x0a87e8cf, new int[] { 205, 1 } },
            { 0x0ae5df54, new int[] { 9999, 6 } },
            { 0x0aec8c97, new int[] { 226, 1 } },
            { 0x0afae8b0, new int[] { 399, 3 } },
            { 0x0b1ad6cc, new int[] { 309, 6 } },
            { 0x0b36e878, new int[] { 155, 5 } },
            { 0x0bfdfb2a, new int[] { 5217, 2 } },
            { 0x0c04ca02, new int[] { 225, 9 } },
            { 0x0c0d4574, new int[] { 135, 7 } },
            { 0x0c189d36, new int[] { 135, 5 } },
            { 0x0ce4a17c, new int[] { 569, 5 } },
            { 0x0d14b885, new int[] { 75, 9 } },
            { 0x0d192399, new int[] { 383, 1 } },
            { 0x0d2dde73, new int[] { 115, 8 } },
            { 0x0d4ce6fd, new int[] { 150, 9 } },
            { 0x0d94642f, new int[] { 345, 8 } },
            { 0x0dfd8714, new int[] { 120, 7 } },
            { 0x0e4f2e14, new int[] { 459, 3 } },
            { 0x0e82c943, new int[] { 300, 0 } },
            { 0x0eb692fb, new int[] { 210, 11 } },
            { 0x0ecbec1c, new int[] { 199, 6 } },
            { 0x0ede95bb, new int[] { 50, 9 } },
            { 0x0f1aa416, new int[] { 39, 6 } },
            { 0x0f77759b, new int[] { 225, 7 } },
            { 0x0f8beda7, new int[] { 109, 0 } },
            { 0x0fbb8bf8, new int[] { 1100, 0 } },
            { 0x0fd4ccef, new int[] { 250, 6 } },
            { 0x0fe5fa64, new int[] { 188, 13 } },
            { 0x0ffc192a, new int[] { 2999, 5 } },
            { 0x10444309, new int[] { 45, 11 } },
            { 0x108262be, new int[] { 79, 7 } },
            { 0x110cf4b1, new int[] { 399, 11 } },
            { 0x112039a0, new int[] { 3500, 6 } },
            { 0x1181ccb1, new int[] { 89, 5 } },
            { 0x11a92d74, new int[] { 50, 6 } },
            { 0x11ec66d9, new int[] { 155, 11 } },
            { 0x12a45904, new int[] { 173, 5 } },
            { 0x12d9e6a3, new int[] { 881, 0 } },
            { 0x135e770f, new int[] { 180, 5 } },
            { 0x14284046, new int[] { 69, 5 } },
            { 0x144a7815, new int[] { 276, 1 } },
            { 0x144c3f7c, new int[] { 35, 5 } },
            { 0x1468c3a7, new int[] { 280, 11 } },
            { 0x14ae1e58, new int[] { 305, 9 } },
            { 0x14ef6729, new int[] { 1950, 6 } },
            { 0x151d17ed, new int[] { 419, 1 } },
            { 0x15316867, new int[] { 90, 5 } },
            { 0x15445522, new int[] { 170, 5 } },
            { 0x154522af, new int[] { 90, 11 } },
            { 0x15460fca, new int[] { 80, 11 } },
            { 0x15597b59, new int[] { 23, 5 } },
            { 0x157021de, new int[] { 99, 5 } },
            { 0x15717e80, new int[] { 325, 9 } },
            { 0x1584c383, new int[] { 1200, 0 } },
            { 0x160bb3d1, new int[] { 2049, 6 } },
            { 0x165cd872, new int[] { 119, 6 } },
            { 0x1671a218, new int[] { 200, 11 } },
            { 0x1694348f, new int[] { 3399, 6 } },
            { 0x16aed8d8, new int[] { 4112, 5 } },
            { 0x16d3a20d, new int[] { 350, 8 } },
            { 0x16f14e74, new int[] { 409, 8 } },
            { 0x171527eb, new int[] { 159, 5 } },
            { 0x17332210, new int[] { 135, 11 } },
            { 0x17579980, new int[] { 3650, 0 } },
            { 0x1772dd4f, new int[] { 9999, 4 } },
            { 0x17f06fe1, new int[] { 111, 5 } },
            { 0x17fc774d, new int[] { 50, 8 } },
            { 0x17fd98f7, new int[] { 3099, 6 } },
            { 0x182e7c24, new int[] { 649, 6 } },
            { 0x19057c09, new int[] { 429, 7 } },
            { 0x191b9479, new int[] { 1980, 5 } },
            { 0x1ab85e45, new int[] { 779, 6 } },
            { 0x1ad5b840, new int[] { 349, 1 } },
            { 0x1b490662, new int[] { 240, 5 } },
            { 0x1b833d24, new int[] { 7999, 3 } },
            { 0x1bb40a4e, new int[] { 565, 6 } },
            { 0x1bbdb732, new int[] { 432, 1 } },
            { 0x1c6b6d6a, new int[] { 850, 5 } },
            { 0x1cc44f98, new int[] { 369, 1 } },
            { 0x1d772052, new int[] { 12999, 6 } },
            { 0x1d8e6e89, new int[] { 92, 1 } },
            { 0x1de172c2, new int[] { 4300, 5 } },
            { 0x1dff3cab, new int[] { 400, 1 } },
            { 0x1e6d621b, new int[] { 520, 5 } },
            { 0x1e743c39, new int[] { 199, 6 } },
            { 0x1e8b9ea7, new int[] { 1250, 3 } },
            { 0x1e8dd05c, new int[] { 14099, 5 } },
            { 0x1f15af7e, new int[] { 1020, 5 } },
            { 0x1f287b65, new int[] { 699, 6 } },
            { 0x1fac199d, new int[] { 375, 2 } },
            { 0x2077e7de, new int[] { 8100, 6 } },
            { 0x2130c4e5, new int[] { 5996, 6 } },
            { 0x21332b58, new int[] { 60, 8 } },
            { 0x21e65bbf, new int[] { 45, 6 } },
            { 0x21f954a6, new int[] { 580, 6 } },
            { 0x2256ba70, new int[] { 55, 9 } },
            { 0x2263dceb, new int[] { 2500, 5 } },
            { 0x228e96d2, new int[] { 99, 0 } },
            { 0x22920f55, new int[] { 450, 0 } },
            { 0x22dd513c, new int[] { 240, 13 } },
            { 0x23941850, new int[] { 300, 8 } },
            { 0x243f0fa0, new int[] { 499, 6 } },
            { 0x246f6a23, new int[] { 0, 6 } },
            { 0x24dad5c4, new int[] { 109, 0 } },
            { 0x2521000e, new int[] { 949, 6 } },
            { 0x253c1a5e, new int[] { 350, 2 } },
            { 0x254233ad, new int[] { 535, 0 } },
            { 0x25b82d98, new int[] { 150, 0 } },
            { 0x25ba0090, new int[] { 169, 1 } },
            { 0x2644d3c7, new int[] { 4700, 2 } },
            { 0x26ad0662, new int[] { 3696, 6 } },
            { 0x26ad56ff, new int[] { 7280, 6 } },
            { 0x26bfbb29, new int[] { 80, 0 } },
            { 0x26d24804, new int[] { 999, 6 } },
            { 0x26e91ef5, new int[] { 2384, 6 } },
            { 0x2768de7d, new int[] { 1715, 0 } },
            { 0x2796b726, new int[] { 429, 0 } },
            { 0x27f8f51f, new int[] { 255, 13 } },
            { 0x28975923, new int[] { 300, 7 } },
            { 0x28a0403e, new int[] { 439, 5 } },
            { 0x2935d383, new int[] { 129, 8 } },
            { 0x294439e2, new int[] { 1500, 1 } },
            { 0x294a48ee, new int[] { 300, 6 } },
            { 0x29cb5a51, new int[] { 1199, 3 } },
            { 0x29eaef1f, new int[] { 131, 7 } },
            { 0x2a262d32, new int[] { 45, 9 } },
            { 0x2a3d8714, new int[] { 89, 6 } },
            { 0x2a7994bf, new int[] { 4260, 5 } },
            { 0x2a7d9829, new int[] { 2333, 5 } },
            { 0x2ab64a1d, new int[] { 249, 1 } },
            { 0x2abc6e73, new int[] { 40, 11 } },
            { 0x2aca7211, new int[] { 150, 8 } },
            { 0x2b4502a2, new int[] { 349, 6 } },
            { 0x2b49a3f4, new int[] { 265, 13 } },
            { 0x2b83b971, new int[] { 300, 11 } },
            { 0x2b89fd8a, new int[] { 239, 13 } },
            { 0x2b956ab9, new int[] { 27999, 6 } },
            { 0x2ba019c7, new int[] { 235, 7 } },
            { 0x2baa0029, new int[] { 89, 6 } },
            { 0x2bb9b580, new int[] { 199, 13 } },
            { 0x2bb9e51d, new int[] { 149, 13 } },
            { 0x2bd867fe, new int[] { 135, 1 } },
            { 0x2be7cbba, new int[] { 149, 0 } },
            { 0x2c501364, new int[] { 350, 7 } },
            { 0x2c555ea3, new int[] { 75, 11 } },
            { 0x2c57d9f1, new int[] { 550, 4 } },
            { 0x2c5ac343, new int[] { 85, 7 } },
            { 0x2c7dff0e, new int[] { 1111, 4 } },
            { 0x2cfb155a, new int[] { 0, 6 } },
            { 0x2d0a3de1, new int[] { 4994, 2 } },
            { 0x2d5f0d6c, new int[] { 455, 6 } },
            { 0x2d773bf9, new int[] { 225, 7 } },
            { 0x2e10faec, new int[] { 329, 6 } },
            { 0x2e453ea5, new int[] { 80, 13 } },
            { 0x2e7f37ea, new int[] { 151, 5 } },
            { 0x2e9fdca5, new int[] { 1222, 0 } },
            { 0x2ecdb068, new int[] { 349, 6 } },
            { 0x2f014f68, new int[] { 135, 13 } },
            { 0x2f42fc26, new int[] { 130, 7 } },
            { 0x2f86506b, new int[] { 5609, 6 } },
            { 0x2f8715d1, new int[] { 699, 9 } },
            { 0x2f92cd8f, new int[] { 99, 5 } },
            { 0x2fc581ad, new int[] { 800, 5 } },
            { 0x2fcd8a30, new int[] { 199, 0 } },
            { 0x2fdf80dd, new int[] { 229, 3 } },
            { 0x2ffaa585, new int[] { 750, 5 } },
            { 0x3007996e, new int[] { 277, 11 } },
            { 0x3026a0f4, new int[] { 260, 11 } },
            { 0x307ddfa6, new int[] { 1200, 10 } },
            { 0x3089d8d2, new int[] { 450, 0 } },
            { 0x30bcda26, new int[] { 300, 2 } },
            { 0x30f4b933, new int[] { 888, 6 } },
            { 0x31a0e42c, new int[] { 300, 14 } },
            { 0x31edeb80, new int[] { 26999, 6 } },
            { 0x3288ed87, new int[] { 1300, 10 } },
            { 0x328ed9e1, new int[] { 240, 0 } },
            { 0x32f5a529, new int[] { 1559, 0 } },
            { 0x3313d86b, new int[] { 229, 13 } },
            { 0x334f732f, new int[] { 340, 0 } },
            { 0x342a5e00, new int[] { 510, 5 } },
            { 0x34431396, new int[] { 33, 11 } },
            { 0x3480731c, new int[] { 425, 1 } },
            { 0x3484c1f7, new int[] { 390, 11 } },
            { 0x34acf253, new int[] { 1789, 5 } },
            { 0x34f7be98, new int[] { 1702, 0 } },
            { 0x34fe4e17, new int[] { 400, 4 } },
            { 0x354a1785, new int[] { 35, 13 } },
            { 0x359e63bd, new int[] { 168, 13 } },
            { 0x35b26d7f, new int[] { 1120, 6 } },
            { 0x35eaebdc, new int[] { 496, 6 } },
            { 0x36113a6d, new int[] { 549, 1 } },
            { 0x3658d1aa, new int[] { 180, 1 } },
            { 0x3659dca2, new int[] { 12, 5 } },
            { 0x3672a8b2, new int[] { 99, 2 } },
            { 0x36f33011, new int[] { 205, 5 } },
            { 0x36fe2161, new int[] { 10, 13 } },
            { 0x37186734, new int[] { 2999, 10 } },
            { 0x371e3b21, new int[] { 389, 0 } },
            { 0x37326795, new int[] { 39, 5 } },
            { 0x3742732c, new int[] { 2000, 1 } },
            { 0x375dd48b, new int[] { 4800, 5 } },
            { 0x3794bcc5, new int[] { 749, 4 } },
            { 0x37bfa925, new int[] { 2000, 5 } },
            { 0x38232e50, new int[] { 699, 0 } },
            { 0x384c487b, new int[] { 215, 5 } },
            { 0x391e258e, new int[] { 450, 1 } },
            { 0x39262c4c, new int[] { 155, 7 } },
            { 0x39319021, new int[] { 210, 11 } },
            { 0x398cc7a0, new int[] { 6600, 5 } },
            { 0x39ce4936, new int[] { 68, 5 } },
            { 0x39dd7192, new int[] { 16000, 5 } },
            { 0x39ed3b5a, new int[] { 313, 5 } },
            { 0x3aa14a49, new int[] { 3999, 6 } },
            { 0x3ac7f3be, new int[] { 939, 6 } },
            { 0x3b0971f3, new int[] { 229, 3 } },
            { 0x3bc18581, new int[] { 88, 5 } },
            { 0x3c30cb5d, new int[] { 811, 0 } },
            { 0x3c52b66d, new int[] { 79, 5 } },
            { 0x3c566968, new int[] { 1200, 4 } },
            { 0x3c71d2e9, new int[] { 300, 0 } },
            { 0x3c9ea694, new int[] { 1200, 10 } },
            { 0x3d1f6466, new int[] { 535, 7 } },
            { 0x3d3e7889, new int[] { 6999, 6 } },
            { 0x3dbfc043, new int[] { 175, 7 } },
            { 0x3dcc1abe, new int[] { 5399, 6 } },
            { 0x3dea8b15, new int[] { 4100, 12 } },
            { 0x3def46a0, new int[] { 2525, 12 } },
            { 0x3e69a01d, new int[] { 611, 0 } },
            { 0x3e6fda2f, new int[] { 250, 1 } },
            { 0x4008dbb7, new int[] { 436, 6 } },
            { 0x404ee381, new int[] { 2488, 2 } },
            { 0x405046ff, new int[] { 90, 6 } },
            { 0x40c778fd, new int[] { 1500, 6 } },
            { 0x4126d70f, new int[] { 80, 7 } },
            { 0x417e691c, new int[] { 515, 5 } },
            { 0x4188ca2b, new int[] { 0, 6 } },
            { 0x41a50757, new int[] { 260, 9 } },
            { 0x41c753c1, new int[] { 65, 7 } },
            { 0x41e1f6f3, new int[] { 27999, 6 } },
            { 0x41fdd8d8, new int[] { 1200, 0 } },
            { 0x4208b8e3, new int[] { 200, 14 } },
            { 0x42197abc, new int[] { 910, 6 } },
            { 0x42316f67, new int[] { 363, 0 } },
            { 0x42bcc380, new int[] { 199, 13 } },
            { 0x42c078b3, new int[] { 1200, 2 } },
            { 0x431d46c8, new int[] { 191, 0 } },
            { 0x4371fde9, new int[] { 155, 5 } },
            { 0x43a116b4, new int[] { 1333, 5 } },
            { 0x43a15ebe, new int[] { 449, 5 } },
            { 0x43b62717, new int[] { 625, 5 } },
            { 0x440087f3, new int[] { 356, 11 } },
            { 0x4450e4e0, new int[] { 5999, 6 } },
            { 0x44e8992a, new int[] { 50, 9 } },
            { 0x4596ee35, new int[] { 325, 6 } },
            { 0x45caf478, new int[] { 1984, 2 } },
            { 0x4699596d, new int[] { 3444, 2 } },
            { 0x46ddd450, new int[] { 413, 4 } },
            { 0x46e807b4, new int[] { 329, 7 } },
            { 0x46ef5577, new int[] { 199, 7 } },
            { 0x46f4e80b, new int[] { 100, 3 } },
            { 0x471a0bf6, new int[] { 4700, 5 } },
            { 0x473532d6, new int[] { 199, 13 } },
            { 0x47a99575, new int[] { 2415, 6 } },
            { 0x47e917c4, new int[] { 800, 1 } },
            { 0x47f39f51, new int[] { 4200, 6 } },
            { 0x481a74ec, new int[] { 1800, 3 } },
            { 0x48a153ad, new int[] { 11000, 5 } },
            { 0x49402e4f, new int[] { 72, 5 } },
            { 0x497a0c3e, new int[] { 600, 12 } },
            { 0x49f9c16f, new int[] { 65, 6 } },
            { 0x4a21d3a7, new int[] { 379, 1 } },
            { 0x4a459a5d, new int[] { 360, 7 } },
            { 0x4a4d1966, new int[] { 225, 8 } },
            { 0x4a70df92, new int[] { 0, 6 } },
            { 0x4a8ae8fb, new int[] { 303, 1 } },
            { 0x4ae4f77c, new int[] { 15000, 5 } },
            { 0x4af2067e, new int[] { 74, 5 } },
            { 0x4b4d30a9, new int[] { 200, 11 } },
            { 0x4b69ea5a, new int[] { 12648, 5 } },
            { 0x4b78bf0e, new int[] { 259, 13 } },
            { 0x4bd0b785, new int[] { 113, 13 } },
            { 0x4be4809a, new int[] { 27999, 6 } },
            { 0x4c103662, new int[] { 715, 4 } },
            { 0x4c442733, new int[] { 139, 1 } },
            { 0x4c443fa4, new int[] { 209, 1 } },
            { 0x4c446f39, new int[] { 335, 1 } },
            { 0x4c659e7d, new int[] { 99, 5 } },
            { 0x4c727c5d, new int[] { 250, 4 } },
            { 0x4cd0523d, new int[] { 399, 6 } },
            { 0x4d6da494, new int[] { 17999, 6 } },
            { 0x4ddf498c, new int[] { 720, 6 } },
            { 0x4deefc68, new int[] { 213, 6 } },
            { 0x4e1ea389, new int[] { 173, 7 } },
            { 0x4e665bf5, new int[] { 200, 7 } },
            { 0x4ee8b3cf, new int[] { 600, 4 } },
            { 0x4ef4ea16, new int[] { 150, 0 } },
            { 0x4ef80f89, new int[] { 515, 6 } },
            { 0x4f51d969, new int[] { 70, 13 } },
            { 0x4f8e1c8b, new int[] { 145, 7 } },
            { 0x4fa133c9, new int[] { 165, 9 } },
            { 0x4fdec997, new int[] { 0, 6 } },
            { 0x502367de, new int[] { 460, 0 } },
            { 0x50b9bc6d, new int[] { 169, 5 } },
            { 0x51474702, new int[] { 225, 11 } },
            { 0x5152a864, new int[] { 175, 6 } },
            { 0x5156efbc, new int[] { 4766, 6 } },
            { 0x515e8635, new int[] { 102, 6 } },
            { 0x5199e127, new int[] { 110, 9 } },
            { 0x51a767e2, new int[] { 90, 9 } },
            { 0x51c23b85, new int[] { 210, 13 } },
            { 0x52112235, new int[] { 1450, 5 } },
            { 0x5249b60e, new int[] { 303, 6 } },
            { 0x5250b4c1, new int[] { 360, 5 } },
            { 0x5295a7a8, new int[] { 1400, 10 } },
            { 0x52ae7a4e, new int[] { 4447, 4 } },
            { 0x52c81f9b, new int[] { 39, 5 } },
            { 0x52ed4b7c, new int[] { 105, 11 } },
            { 0x52f6acd7, new int[] { 236, 6 } },
            { 0x52f83bea, new int[] { 199, 6 } },
            { 0x5359bfc1, new int[] { 165, 5 } },
            { 0x536a3e4d, new int[] { 120, 3 } },
            { 0x547f4934, new int[] { 699, 6 } },
            { 0x54b3b81a, new int[] { 4715, 6 } },
            { 0x54c3e0cf, new int[] { 65, 11 } },
            { 0x54c7f70d, new int[] { 2099, 3 } },
            { 0x552a8297, new int[] { 129, 6 } },
            { 0x55acb9e0, new int[] { 699, 8 } },
            { 0x55bcbc04, new int[] { 1115, 0 } },
            { 0x56007c4b, new int[] { 120, 8 } },
            { 0x56276590, new int[] { 4499, 2 } },
            { 0x578f5050, new int[] { 8200, 6 } },
            { 0x57f68405, new int[] { 75, 5 } },
            { 0x582f5936, new int[] { 633, 4 } },
            { 0x5880d445, new int[] { 8100, 5 } },
            { 0x58dde126, new int[] { 7999, 4 } },
            { 0x59132604, new int[] { 10000, 5 } },
            { 0x59133e93, new int[] { 590, 5 } },
            { 0x594a89f0, new int[] { 459, 3 } },
            { 0x59b200b5, new int[] { 30, 7 } },
            { 0x59c389bc, new int[] { 100, 0 } },
            { 0x5a01eb5b, new int[] { 979, 6 } },
            { 0x5a12bc70, new int[] { 85, 13 } },
            { 0x5a1bc4bc, new int[] { 1529, 6 } },
            { 0x5a5cda09, new int[] { 95, 1 } },
            { 0x5a6feced, new int[] { 70, 13 } },
            { 0x5a91cee0, new int[] { 1015, 6 } },
            { 0x5b048a52, new int[] { 95, 7 } },
            { 0x5b088f5b, new int[] { 349, 13 } },
            { 0x5b182c35, new int[] { 2499, 4 } },
            { 0x5b561459, new int[] { 99, 9 } },
            { 0x5bfb6140, new int[] { 8511, 4 } },
            { 0x5c198cbe, new int[] { 275, 11 } },
            { 0x5cb56b2d, new int[] { 10229, 5 } },
            { 0x5cdc712f, new int[] { 500, 6 } },
            { 0x5d071f26, new int[] { 135, 7 } },
            { 0x5d0bbdb6, new int[] { 390, 5 } },
            { 0x5d39291c, new int[] { 119, 9 } },
            { 0x5d6b538a, new int[] { 2000, 12 } },
            { 0x5d723615, new int[] { 10, 11 } },
            { 0x5d767e1f, new int[] { 5, 11 } },
            { 0x5d7b6688, new int[] { 25, 11 } },
            { 0x5dac929f, new int[] { 0, 6 } },
            { 0x5e8b157a, new int[] { 5678, 3 } },
            { 0x5e9e42fc, new int[] { 6199, 6 } },
            { 0x5edb929d, new int[] { 198, 5 } },
            { 0x5ede0bc4, new int[] { 400, 0 } },
            { 0x5f421524, new int[] { 160, 11 } },
            { 0x5fa381c1, new int[] { 500, 3 } },
            { 0x5faa302a, new int[] { 922, 6 } },
            { 0x5fb93288, new int[] { 777, 5 } },
            { 0x601b0c9a, new int[] { 1300, 5 } },
            { 0x6065ac2d, new int[] { 135, 9 } },
            { 0x60909779, new int[] { 415, 8 } },
            { 0x60d99178, new int[] { 579, 6 } },
            { 0x60e23da6, new int[] { 521, 1 } },
            { 0x6106a1a7, new int[] { 537, 1 } },
            { 0x615c9169, new int[] { 179, 9 } },
            { 0x6188f5d7, new int[] { 1400, 6 } },
            { 0x61b7982e, new int[] { 235, 13 } },
            { 0x629be21f, new int[] { 575, 2 } },
            { 0x62b6f3c1, new int[] { 450, 5 } },
            { 0x62ccb570, new int[] { 99, 6 } },
            { 0x62f8a7e4, new int[] { 399, 8 } },
            { 0x6350e96f, new int[] { 6998, 6 } },
            { 0x637692ea, new int[] { 1612, 6 } },
            { 0x63a3d7d4, new int[] { 139, 8 } },
            { 0x645365da, new int[] { 335, 0 } },
            { 0x645542df, new int[] { 19999, 6 } },
            { 0x64983e4d, new int[] { 65, 6 } },
            { 0x64a10860, new int[] { 149, 9 } },
            { 0x64f0e2bc, new int[] { 99, 5 } },
            { 0x65274a4f, new int[] { 25, 11 } },
            { 0x65396e67, new int[] { 733, 5 } },
            { 0x6585d104, new int[] { 329, 0 } },
            { 0x6593d291, new int[] { 3000, 0 } },
            { 0x65cb152b, new int[] { 193, 6 } },
            { 0x66025e42, new int[] { 349, 8 } },
            { 0x6610c43c, new int[] { 240, 7 } },
            { 0x66131ba8, new int[] { 369, 6 } },
            { 0x66237c5d, new int[] { 500, 0 } },
            { 0x663eaffc, new int[] { 4500, 0 } },
            { 0x66548be0, new int[] { 1511, 0 } },
            { 0x66877bef, new int[] { 6999, 6 } },
            { 0x6691c3d8, new int[] { 8000, 6 } },
            { 0x67406e73, new int[] { 1555, 5 } },
            { 0x675c18af, new int[] { 600, 2 } },
            { 0x678f09ff, new int[] { 310, 11 } },
            { 0x6854da61, new int[] { 305, 11 } },
            { 0x68897ff8, new int[] { 899, 2 } },
            { 0x68af92a3, new int[] { 235, 6 } },
            { 0x68c11912, new int[] { 645, 1 } },
            { 0x6923633a, new int[] { 275, 3 } },
            { 0x6928408e, new int[] { 69, 5 } },
            { 0x6954e030, new int[] { 551, 3 } },
            { 0x69bd37af, new int[] { 6000, 10 } },
            { 0x6a3889f8, new int[] { 600, 0 } },
            { 0x6a53de3c, new int[] { 3899, 6 } },
            { 0x6a55e034, new int[] { 199, 5 } },
            { 0x6a776e73, new int[] { 1199, 3 } },
            { 0x6a8594ad, new int[] { 4413, 6 } },
            { 0x6ad59dd3, new int[] { 449, 1 } },
            { 0x6aeb3d99, new int[] { 1300, 6 } },
            { 0x6b264238, new int[] { 75, 3 } },
            { 0x6bad868b, new int[] { 130, 5 } },
            { 0x6bf6d402, new int[] { 95, 0 } },
            { 0x6c1bb706, new int[] { 760, 6 } },
            { 0x6c3bbc3e, new int[] { 265, 7 } },
            { 0x6c582f42, new int[] { 450, 4 } },
            { 0x6c7caec2, new int[] { 200, 7 } },
            { 0x6c86123e, new int[] { 49, 5 } },
            { 0x6c8bd4a3, new int[] { 75, 8 } },
            { 0x6c9f7100, new int[] { 937, 5 } },
            { 0x6cb7f434, new int[] { 345, 7 } },
            { 0x6cc079c2, new int[] { 319, 0 } },
            { 0x6d96dcfa, new int[] { 900, 10 } },
            { 0x6da8b998, new int[] { 125, 0 } },
            { 0x6dd88e6d, new int[] { 111, 3 } },
            { 0x6e0ec37e, new int[] { 4999, 4 } },
            { 0x6e325f22, new int[] { 9099, 5 } },
            { 0x6f2bb8ed, new int[] { 180, 8 } },
            { 0x6f35df40, new int[] { 75, 8 } },
            { 0x6f93a84a, new int[] { 100, 13 } },
            { 0x6fb00726, new int[] { 622, 4 } },
            { 0x702851b4, new int[] { 11111, 5 } },
            { 0x70467b85, new int[] { 10000, 6 } },
            { 0x7051ee96, new int[] { 570, 6 } },
            { 0x70760da9, new int[] { 900, 5 } },
            { 0x70b0d5f2, new int[] { 1099, 6 } },
            { 0x70fc19c3, new int[] { 333, 5 } },
            { 0x718ea9c7, new int[] { 75, 5 } },
            { 0x722a0fbf, new int[] { 8439, 6 } },
            { 0x724ca405, new int[] { 70, 5 } },
            { 0x7280cbfc, new int[] { 175, 0 } },
            { 0x72a5cbe3, new int[] { 349, 6 } },
            { 0x72bdbb7d, new int[] { 70, 11 } },
            { 0x72fa6739, new int[] { 417, 6 } },
            { 0x7318c9df, new int[] { 0, 6 } },
            { 0x7387f3a2, new int[] { 519, 2 } },
            { 0x738a419a, new int[] { 399, 0 } },
            { 0x73d6180d, new int[] { 800, 1 } },
            { 0x73eef299, new int[] { 899, 10 } },
            { 0x74d93e95, new int[] { 215, 1 } },
            { 0x74e7ffa9, new int[] { 553, 3 } },
            { 0x75ac188c, new int[] { 500, 4 } },
            { 0x75b0b2b7, new int[] { 25, 13 } },
            { 0x765bcec0, new int[] { 447, 6 } },
            { 0x7726e3a6, new int[] { 550, 6 } },
            { 0x77d26881, new int[] { 419, 14 } },
            { 0x77d429bb, new int[] { 63, 7 } },
            { 0x77d9944d, new int[] { 399, 1 } },
            { 0x7839c094, new int[] { 275, 5 } },
            { 0x78d7ea4d, new int[] { 5000, 6 } },
            { 0x78edcd25, new int[] { 30, 5 } },
            { 0x7939d97d, new int[] { 40, 1 } },
            { 0x795e70b6, new int[] { 1200, 0 } },
            { 0x79e2bb28, new int[] { 150, 9 } },
            { 0x79fa7e6b, new int[] { 460, 5 } },
            { 0x7a56922f, new int[] { 999, 6 } },
            { 0x7a8fdbf4, new int[] { 2483, 6 } },
            { 0x7ab03de4, new int[] { 4300, 5 } },
            { 0x7b21053a, new int[] { 15000, 3 } },
            { 0x7b6c8c63, new int[] { 235, 1 } },
            { 0x7bea0977, new int[] { 0, 6 } },
            { 0x7bfdcc88, new int[] { 2012, 6 } },
            { 0x7c21272e, new int[] { 1001, 0 } },
            { 0x7c24a376, new int[] { 111, 11 } },
            { 0x7c5e5c6d, new int[] { 411, 0 } },
            { 0x7cab54a2, new int[] { 210, 8 } },
            { 0x7cb11019, new int[] { 250, 0 } },
            { 0x7d2089b6, new int[] { 45, 5 } },
            { 0x7d2995e4, new int[] { 99, 7 } },
            { 0x7d4a0f76, new int[] { 249, 2 } },
            { 0x7d9b1a47, new int[] { 63, 9 } },
            { 0x7e09517e, new int[] { 135, 7 } },
            { 0x7e1f25b1, new int[] { 1200, 6 } },
            { 0x7e56b1be, new int[] { 445, 7 } },
            { 0x7e853dea, new int[] { 99, 3 } },
            { 0x7e97444a, new int[] { 180, 0 } },
            { 0x7ed5d1b6, new int[] { 319, 4 } },
            { 0x7ef99b7e, new int[] { 1450, 6 } },
            { 0x7f0abd26, new int[] { 150, 9 } },
            { 0x7f1ae4cf, new int[] { 99, 5 } },
            { 0x7f283620, new int[] { 850, 1 } },
            { 0x7f775eee, new int[] { 2339, 6 } },
            { 0x7fab4493, new int[] { 135, 7 } },
            { 0x7fbe5529, new int[] { 35, 7 } },
            { 0x7fd422a4, new int[] { 85, 7 } },
            { 0x7fdf0fc1, new int[] { 110, 7 } },
            { 0x801f7235, new int[] { 270, 1 } },
            { 0x80858fd1, new int[] { 299, 2 } },
            { 0x81179ab3, new int[] { 450, 0 } },
            { 0x813a5a6b, new int[] { 30, 6 } },
            { 0x81a87bab, new int[] { 899, 5 } },
            { 0x81b3a551, new int[] { 299, 6 } },
            { 0x81c7a5e0, new int[] { 130, 0 } },
            { 0x81d97262, new int[] { 1672, 3 } },
            { 0x823a9ee0, new int[] { 190, 0 } },
            { 0x82e04c5b, new int[] { 1000, 0 } },
            { 0x82e0f146, new int[] { 80, 0 } },
            { 0x83023da6, new int[] { 2000, 4 } },
            { 0x835ba084, new int[] { 1800, 3 } },
            { 0x83e0af58, new int[] { 30, 6 } },
            { 0x83eb896c, new int[] { 25, 13 } },
            { 0x849207af, new int[] { 2950, 2 } },
            { 0x84ceb10b, new int[] { 300, 6 } },
            { 0x84e0774c, new int[] { 300, 4 } },
            { 0x852df95d, new int[] { 259, 4 } },
            { 0x855cde99, new int[] { 950, 5 } },
            { 0x8592f25b, new int[] { 879, 6 } },
            { 0x85b424a2, new int[] { 794, 6 } },
            { 0x85b46ca8, new int[] { 3000, 6 } },
            { 0x85e00942, new int[] { 800, 4 } },
            { 0x85e02a40, new int[] { 650, 4 } },
            { 0x85e4adbe, new int[] { 551, 3 } },
            { 0x85f1a085, new int[] { 85, 2 } },
            { 0x8613a26a, new int[] { 551, 3 } },
            { 0x862ccc31, new int[] { 175, 9 } },
            { 0x86e0bbb5, new int[] { 50, 3 } },
            { 0x877feca1, new int[] { 13100, 5 } },
            { 0x87c44d57, new int[] { 68, 0 } },
            { 0x87c99127, new int[] { 299, 2 } },
            { 0x87d00adc, new int[] { 1450, 0 } },
            { 0x880bb75e, new int[] { 599, 6 } },
            { 0x88103f3e, new int[] { 16019, 5 } },
            { 0x882480a6, new int[] { 1100, 2 } },
            { 0x88417fac, new int[] { 487, 1 } },
            { 0x8841dbb9, new int[] { 1599, 6 } },
            { 0x88734ce8, new int[] { 24999, 6 } },
            { 0x88b6a098, new int[] { 3799, 10 } },
            { 0x88ede461, new int[] { 500, 8 } },
            { 0x89173b24, new int[] { 395, 5 } },
            { 0x8942bdc7, new int[] { 120, 9 } },
            { 0x8a712a85, new int[] { 81, 5 } },
            { 0x8ad987b8, new int[] { 289, 3 } },
            { 0x8b04d5e1, new int[] { 563, 5 } },
            { 0x8ba56284, new int[] { 229, 11 } },
            { 0x8bd70015, new int[] { 150, 1 } },
            { 0x8bf2e852, new int[] { 160, 9 } },
            { 0x8c44ba58, new int[] { 250, 9 } },
            { 0x8c9350d2, new int[] { 234, 0 } },
            { 0x8c9e33c9, new int[] { 200, 1 } },
            { 0x8cb4d449, new int[] { 333, 8 } },
            { 0x8d1b6101, new int[] { 75, 13 } },
            { 0x8d4ee7df, new int[] { 2555, 5 } },
            { 0x8d70f7b3, new int[] { 60, 5 } },
            { 0x8da9ffb4, new int[] { 210, 3 } },
            { 0x8dab571f, new int[] { 75, 5 } },
            { 0x8e3520bd, new int[] { 194, 2 } },
            { 0x8e880484, new int[] { 281, 8 } },
            { 0x8e8d9ca9, new int[] { 1999, 3 } },
            { 0x8eaa68ad, new int[] { 1000, 12 } },
            { 0x8eb8e1a6, new int[] { 250, 5 } },
            { 0x8ec5750d, new int[] { 149, 6 } },
            { 0x8eec19f7, new int[] { 45, 6 } },
            { 0x8f01bdcb, new int[] { 629, 0 } },
            { 0x8f033944, new int[] { 3500, 5 } },
            { 0x8f300ef1, new int[] { 2100, 6 } },
            { 0x8f667b23, new int[] { 1000, 7 } },
            { 0x8fb1bf00, new int[] { 71, 5 } },
            { 0x8fca5a7b, new int[] { 373, 8 } },
            { 0x8fed54c2, new int[] { 6500, 4 } },
            { 0x8ff0ca39, new int[] { 0, 6 } },
            { 0x9082401f, new int[] { 160, 0 } },
            { 0x90af09f4, new int[] { 139, 5 } },
            { 0x90c1c7cb, new int[] { 999, 6 } },
            { 0x90c7c9e4, new int[] { 0, 6 } },
            { 0x912cbed4, new int[] { 275, 9 } },
            { 0x91767a36, new int[] { 250, 1 } },
            { 0x91ed6984, new int[] { 1750, 3 } },
            { 0x921fda07, new int[] { 200, 1 } },
            { 0x9293b59e, new int[] { 2012, 5 } },
            { 0x92c87dc0, new int[] { 184, 7 } },
            { 0x9367e97a, new int[] { 1990, 5 } },
            { 0x93f98c8a, new int[] { 6000, 5 } },
            { 0x94082f6d, new int[] { 21000, 6 } },
            { 0x948aed3c, new int[] { 1999, 10 } },
            { 0x94aa32f4, new int[] { 500, 6 } },
            { 0x94d2df9d, new int[] { 485, 7 } },
            { 0x94f5cda8, new int[] { 170, 7 } },
            { 0x94fd43dc, new int[] { 639, 5 } },
            { 0x952bcac7, new int[] { 180, 5 } },
            { 0x95624034, new int[] { 39, 5 } },
            { 0x95a59fd2, new int[] { 115, 9 } },
            { 0x95b09639, new int[] { 649, 6 } },
            { 0x95d4de33, new int[] { 699, 6 } },
            { 0x95efe015, new int[] { 1750, 5 } },
            { 0x9667a489, new int[] { 850, 10 } },
            { 0x969bde47, new int[] { 333, 5 } },
            { 0x96a5928b, new int[] { 699, 1 } },
            { 0x96c58db2, new int[] { 311, 4 } },
            { 0x96cb67ae, new int[] { 599, 6 } },
            { 0x96e3398d, new int[] { 5, 11 } },
            { 0x97a845c3, new int[] { 120, 6 } },
            { 0x984671a9, new int[] { 300, 5 } },
            { 0x98a0a6d6, new int[] { 35, 7 } },
            { 0x98e09eb3, new int[] { 50, 7 } },
            { 0x98e0f8bd, new int[] { 200, 5 } },
            { 0x98e9cfe2, new int[] { 1100, 6 } },
            { 0x99120547, new int[] { 500, 0 } },
            { 0x99400b4a, new int[] { 215, 11 } },
            { 0x994fb8f8, new int[] { 335, 0 } },
            { 0x996de865, new int[] { 840, 0 } },
            { 0x997beef4, new int[] { 560, 6 } },
            { 0x9985a06f, new int[] { 1440, 0 } },
            { 0x99e0a9b1, new int[] { 120, 1 } },
            { 0x9a0d8249, new int[] { 100, 8 } },
            { 0x9a37fba7, new int[] { 49, 13 } },
            { 0x9aa2312f, new int[] { 4199, 6 } },
            { 0x9ae018bb, new int[] { 120, 5 } },
            { 0x9b3340b9, new int[] { 1199, 6 } },
            { 0x9b3a5cdf, new int[] { 250, 6 } },
            { 0x9b3cdd30, new int[] { 275, 0 } },
            { 0x9be0e2bf, new int[] { 200, 0 } },
            { 0x9c223167, new int[] { 365, 7 } },
            { 0x9c541464, new int[] { 24, 13 } },
            { 0x9c59140e, new int[] { 896, 1 } },
            { 0x9cb1bb9b, new int[] { 1749, 3 } },
            { 0x9d4b03a5, new int[] { 125, 6 } },
            { 0x9da7b210, new int[] { 327, 11 } },
            { 0x9ede28ae, new int[] { 70, 13 } },
            { 0x9ee7eb60, new int[] { 260, 8 } },
            { 0x9f12673e, new int[] { 450, 0 } },
            { 0x9f3a38b9, new int[] { 425, 1 } },
            { 0x9f7295a9, new int[] { 3499, 6 } },
            { 0x9f733d4f, new int[] { 1984, 2 } },
            { 0x9fa148dc, new int[] { 220, 2 } },
            { 0x9fb223ce, new int[] { 250, 6 } },
            { 0x9fd45e39, new int[] { 4000, 5 } },
            { 0xa01e500d, new int[] { 950, 2 } },
            { 0xa022c70c, new int[] { 75, 7 } },
            { 0xa02c8857, new int[] { 290, 11 } },
            { 0xa072951f, new int[] { 269, 1 } },
            { 0xa08dd989, new int[] { 439, 7 } },
            { 0xa08e0c14, new int[] { 1849, 0 } },
            { 0xa0af4e74, new int[] { 5449, 5 } },
            { 0xa0ee756f, new int[] { 65, 5 } },
            { 0xa11d2875, new int[] { 8100, 5 } },
            { 0xa13f39cf, new int[] { 1800, 5 } },
            { 0xa1594e42, new int[] { 1100, 5 } },
            { 0xa1755ff7, new int[] { 191, 1 } },
            { 0xa1bd4c80, new int[] { 60, 5 } },
            { 0xa1d5048a, new int[] { 5000, 5 } },
            { 0xa1d6fe56, new int[] { 253, 0 } },
            { 0xa22c5865, new int[] { 1100, 7 } },
            { 0xa2666952, new int[] { 60, 5 } },
            { 0xa2daa08c, new int[] { 10, 11 } },
            { 0xa2fe803a, new int[] { 2699, 10 } },
            { 0xa46aed33, new int[] { 410, 1 } },
            { 0xa478d407, new int[] { 650, 5 } },
            { 0xa483d725, new int[] { 30, 3 } },
            { 0xa489640d, new int[] { 0, 6 } },
            { 0xa4d8c299, new int[] { 287, 0 } },
            { 0xa4e75cff, new int[] { 99, 7 } },
            { 0xa5004e8c, new int[] { 8000, 6 } },
            { 0xa5b9515a, new int[] { 2199, 1 } },
            { 0xa5c0bb1e, new int[] { 399, 6 } },
            { 0xa607cc98, new int[] { 2500, 6 } },
            { 0xa67ac823, new int[] { 60, 9 } },
            { 0xa6a4a1ac, new int[] { 0, 6 } },
            { 0xa6aebf5b, new int[] { 250, 7 } },
            { 0xa6caf1dd, new int[] { 510, 6 } },
            { 0xa7340ec3, new int[] { 699, 4 } },
            { 0xa77f36ae, new int[] { 1, 6 } },
            { 0xa7a70f83, new int[] { 7439, 6 } },
            { 0xa805e814, new int[] { 580, 5 } },
            { 0xa82ca08e, new int[] { 700, 5 } },
            { 0xa8ad8714, new int[] { 125, 13 } },
            { 0xa8bdd25b, new int[] { 5100, 6 } },
            { 0xa8c59f83, new int[] { 110, 13 } },
            { 0xa90a7d01, new int[] { 800, 1 } },
            { 0xa91403ab, new int[] { 935, 6 } },
            { 0xa91d4963, new int[] { 925, 6 } },
            { 0xa94c3259, new int[] { 499, 0 } },
            { 0xa9c05832, new int[] { 6888, 6 } },
            { 0xa9ec8419, new int[] { 1350, 0 } },
            { 0xaa2f9672, new int[] { 2550, 3 } },
            { 0xaa655e9f, new int[] { 230, 13 } },
            { 0xaa75a097, new int[] { 100, 13 } },
            { 0xaa86ad2a, new int[] { 315, 11 } },
            { 0xaae98936, new int[] { 21999, 6 } },
            { 0xaaf447d6, new int[] { 999, 1 } },
            { 0xab101956, new int[] { 245, 1 } },
            { 0xab647747, new int[] { 250, 3 } },
            { 0xabe586cd, new int[] { 2140, 5 } },
            { 0xabfc34d3, new int[] { 924, 0 } },
            { 0xabfc4be9, new int[] { 239, 0 } },
            { 0xac262843, new int[] { 35, 6 } },
            { 0xac66d6ca, new int[] { 299, 11 } },
            { 0xacee4ba5, new int[] { 4999, 5 } },
            { 0xad005811, new int[] { 1112, 6 } },
            { 0xad08fdc0, new int[] { 100, 2 } },
            { 0xad0b9dd6, new int[] { 815, 6 } },
            { 0xad12e0d0, new int[] { 35, 5 } },
            { 0xad2c5018, new int[] { 159, 5 } },
            { 0xad4cf878, new int[] { 499, 3 } },
            { 0xad4d0d62, new int[] { 360, 0 } },
            { 0xad591aac, new int[] { 99, 5 } },
            { 0xad67a694, new int[] { 99, 5 } },
            { 0xad67be03, new int[] { 99, 5 } },
            { 0xad67ee9e, new int[] { 99, 5 } },
            { 0xae12c97a, new int[] { 767, 1 } },
            { 0xae1daf72, new int[] { 179, 0 } },
            { 0xae5c99e7, new int[] { 299, 1 } },
            { 0xae6e43d6, new int[] { 208, 5 } },
            { 0xaed36fcb, new int[] { 342, 2 } },
            { 0xaed3c88a, new int[] { 96, 11 } },
            { 0xaf4a1300, new int[] { 150, 5 } },
            { 0xaf85a57c, new int[] { 1150, 2 } },
            { 0xafabcc3d, new int[] { 6500, 3 } },
            { 0xb01c2620, new int[] { 99, 5 } },
            { 0xb0706e2a, new int[] { 99, 5 } },
            { 0xb1153e7d, new int[] { 39, 11 } },
            { 0xb1230866, new int[] { 4545, 2 } },
            { 0xb159c37d, new int[] { 335, 5 } },
            { 0xb1b176bd, new int[] { 99, 5 } },
            { 0xb21b9510, new int[] { 319, 7 } },
            { 0xb26ab256, new int[] { 2099, 6 } },
            { 0xb31edff8, new int[] { 5499, 5 } },
            { 0xb346903c, new int[] { 875, 5 } },
            { 0xb3760ba8, new int[] { 111, 5 } },
            { 0xb407a783, new int[] { 133, 5 } },
            { 0xb46b0e55, new int[] { 588, 5 } },
            { 0xb48a1144, new int[] { 1470, 5 } },
            { 0xb499ec29, new int[] { 955, 3 } },
            { 0xb4b333e4, new int[] { 89, 5 } },
            { 0xb5215809, new int[] { 3000, 12 } },
            { 0xb5250c97, new int[] { 2699, 5 } },
            { 0xb53903aa, new int[] { 70, 9 } },
            { 0xb56a0aff, new int[] { 1299, 6 } },
            { 0xb6236b64, new int[] { 155, 0 } },
            { 0xb62454df, new int[] { 780, 6 } },
            { 0xb668dd56, new int[] { 199, 8 } },
            { 0xb78e7f8d, new int[] { 9009, 5 } },
            { 0xb790a819, new int[] { 350, 8 } },
            { 0xb796426e, new int[] { 33, 5 } },
            { 0xb797465f, new int[] { 3500, 5 } },
            { 0xb79f3182, new int[] { 239, 5 } },
            { 0xb7cd406c, new int[] { 350, 4 } },
            { 0xb7eaacba, new int[] { 250, 8 } },
            { 0xb80950cc, new int[] { 26999, 6 } },
            { 0xb867117e, new int[] { 80, 3 } },
            { 0xb87a8d3f, new int[] { 1150, 7 } },
            { 0xb89b2451, new int[] { 7647, 5 } },
            { 0xb9020120, new int[] { 300, 11 } },
            { 0xb94755df, new int[] { 2500, 2 } },
            { 0xb99d5c6e, new int[] { 1600, 0 } },
            { 0xb9b0c862, new int[] { 250, 13 } },
            { 0xba67ead2, new int[] { 50, 7 } },
            { 0xba6e27bb, new int[] { 335, 9 } },
            { 0xba71a336, new int[] { 40, 7 } },
            { 0xba7e8047, new int[] { 234, 1 } },
            { 0xba9dc16a, new int[] { 650, 6 } },
            { 0xbb0846ea, new int[] { 13399, 5 } },
            { 0xbb1bcf68, new int[] { 189, 1 } },
            { 0xbb626ef3, new int[] { 250, 2 } },
            { 0xbb83a08a, new int[] { 150, 11 } },
            { 0xbba46c20, new int[] { 15, 5 } },
            { 0xbc12cd61, new int[] { 129, 5 } },
            { 0xbc3ba088, new int[] { 7600, 5 } },
            { 0xbc4b1c4f, new int[] { 2181, 0 } },
            { 0xbc4b7a23, new int[] { 787, 0 } },
            { 0xbc537c52, new int[] { 245, 6 } },
            { 0xbc661500, new int[] { 99, 5 } },
            { 0xbcab486a, new int[] { 235, 0 } },
            { 0xbcc81aff, new int[] { 73, 9 } },
            { 0xbd78c75d, new int[] { 199, 5 } },
            { 0xbdcf6469, new int[] { 900, 10 } },
            { 0xbde052c5, new int[] { 90, 8 } },
            { 0xbe1d7003, new int[] { 60, 5 } },
            { 0xbe773f58, new int[] { 80, 6 } },
            { 0xbeb0f165, new int[] { 500, 9 } },
            { 0xbedd7b26, new int[] { 139, 6 } },
            { 0xbf137195, new int[] { 450, 2 } },
            { 0xbf1b918e, new int[] { 1000, 10 } },
            { 0xbf51b5f3, new int[] { 121, 0 } },
            { 0xbfc58dd0, new int[] { 3999, 6 } },
            { 0xc02b406a, new int[] { 10, 11 } },
            { 0xc041ed5b, new int[] { 100, 6 } },
            { 0xc0e20936, new int[] { 1549, 5 } },
            { 0xc0e64804, new int[] { 319, 0 } },
            { 0xc192dacc, new int[] { 85, 3 } },
            { 0xc1b6da6a, new int[] { 450, 1 } },
            { 0xc25fcf86, new int[] { 295, 7 } },
            { 0xc26265b3, new int[] { 8200, 5 } },
            { 0xc2a91fd0, new int[] { 800, 6 } },
            { 0xc2f1a050, new int[] { 251, 0 } },
            { 0xc349c14c, new int[] { 490, 6 } },
            { 0xc3927fa3, new int[] { 400, 8 } },
            { 0xc3efd759, new int[] { 115, 5 } },
            { 0xc4370e53, new int[] { 888, 6 } },
            { 0xc4387388, new int[] { 522, 8 } },
            { 0xc48f31a0, new int[] { 2000, 0 } },
            { 0xc4c35536, new int[] { 233, 5 } },
            { 0xc523d73d, new int[] { 329, 6 } },
            { 0xc56562c1, new int[] { 277, 8 } },
            { 0xc58b5c78, new int[] { 1445, 0 } },
            { 0xc5d8a768, new int[] { 200, 9 } },
            { 0xc606154b, new int[] { 230, 6 } },
            { 0xc6448ae4, new int[] { 145, 9 } },
            { 0xc69b6e73, new int[] { 65, 8 } },
            { 0xc6b2bdd5, new int[] { 270, 13 } },
            { 0xc71e43cd, new int[] { 35, 5 } },
            { 0xc73c14a4, new int[] { 290, 5 } },
            { 0xc74e7da7, new int[] { 4312, 6 } },
            { 0xc7a7215c, new int[] { 3999, 5 } },
            { 0xc7d177e1, new int[] { 66, 5 } },
            { 0xc7d6cf27, new int[] { 25, 7 } },
            { 0xc7efe216, new int[] { 399, 6 } },
            { 0xc884fc9e, new int[] { 80, 7 } },
            { 0xc8ebdd42, new int[] { 150, 1 } },
            { 0xc96b93eb, new int[] { 120, 6 } },
            { 0xc99ac74e, new int[] { 179, 11 } },
            { 0xca528b1d, new int[] { 99, 5 } },
            { 0xca55ed92, new int[] { 537, 5 } },
            { 0xcaf38382, new int[] { 203, 0 } },
            { 0xcb386e73, new int[] { 199, 7 } },
            { 0xcb6e77ab, new int[] { 60, 1 } },
            { 0xcb790e58, new int[] { 300, 9 } },
            { 0xcbc6c317, new int[] { 99, 5 } },
            { 0xcbd2be4e, new int[] { 1300, 7 } },
            { 0xcbe53119, new int[] { 315, 0 } },
            { 0xcbef938d, new int[] { 4888, 2 } },
            { 0xcbeff420, new int[] { 6714, 2 } },
            { 0xcc66ad61, new int[] { 4481, 6 } },
            { 0xcc944729, new int[] { 55, 1 } },
            { 0xcce4be4f, new int[] { 399, 6 } },
            { 0xcd39bb3a, new int[] { 250, 7 } },
            { 0xcda8d70a, new int[] { 145, 1 } },
            { 0xcdd887b1, new int[] { 2750, 2 } },
            { 0xce10c801, new int[] { 381, 8 } },
            { 0xce3615a5, new int[] { 159, 6 } },
            { 0xce47dd58, new int[] { 10, 6 } },
            { 0xce4b38cf, new int[] { 15, 6 } },
            { 0xce4d6011, new int[] { 140, 8 } },
            { 0xce6c2041, new int[] { 220, 8 } },
            { 0xce6c4713, new int[] { 140, 8 } },
            { 0xceb2f9b8, new int[] { 400, 11 } },
            { 0xcf000516, new int[] { 210, 1 } },
            { 0xcf18067e, new int[] { 449, 8 } },
            { 0xcf5a1a2f, new int[] { 17777, 6 } },
            { 0xcf5b61d3, new int[] { 243, 8 } },
            { 0xcf62b688, new int[] { 380, 1 } },
            { 0xcf89e47b, new int[] { 399, 5 } },
            { 0xcfd10860, new int[] { 19, 6 } },
            { 0xcfe94d2c, new int[] { 289, 8 } },
            { 0xd0a0f269, new int[] { 562, 6 } },
            { 0xd0a244e4, new int[] { 68, 9 } },
            { 0xd0b754e0, new int[] { 180, 9 } },
            { 0xd1180b2a, new int[] { 1100, 0 } },
            { 0xd158b046, new int[] { 120, 9 } },
            { 0xd191aff7, new int[] { 999, 3 } },
            { 0xd191ff6a, new int[] { 2800, 3 } },
            { 0xd1bc4074, new int[] { 350, 4 } },
            { 0xd1ef8174, new int[] { 50, 6 } },
            { 0xd2944a33, new int[] { 65, 0 } },
            { 0xd2a03993, new int[] { 2300, 3 } },
            { 0xd2bb9953, new int[] { 502, 6 } },
            { 0xd389c839, new int[] { 40, 6 } },
            { 0xd3a28b82, new int[] { 359, 1 } },
            { 0xd4588282, new int[] { 139, 0 } },
            { 0xd45e4bea, new int[] { 379, 6 } },
            { 0xd4709f75, new int[] { 400, 8 } },
            { 0xd49346ff, new int[] { 650, 3 } },
            { 0xd4bf28f3, new int[] { 879, 0 } },
            { 0xd5a34f0f, new int[] { 260, 1 } },
            { 0xd5ca5425, new int[] { 100, 11 } },
            { 0xd608b270, new int[] { 111, 5 } },
            { 0xd68e73d8, new int[] { 180, 11 } },
            { 0xd735a326, new int[] { 80, 9 } },
            { 0xd73d1f82, new int[] { 489, 0 } },
            { 0xd7404661, new int[] { 59, 6 } },
            { 0xd84d39ce, new int[] { 275, 1 } },
            { 0xd8bb3aa8, new int[] { 250, 0 } },
            { 0xd95f52ef, new int[] { 88, 1 } },
            { 0xd97681db, new int[] { 1200, 0 } },
            { 0xd985a7ad, new int[] { 599, 6 } },
            { 0xd9a73952, new int[] { 80, 1 } },
            { 0xd9ae1ae5, new int[] { 215, 1 } },
            { 0xd9bd21cd, new int[] { 295, 11 } },
            { 0xd9e3bf7b, new int[] { 3200, 5 } },
            { 0xda2b8f79, new int[] { 920, 6 } },
            { 0xda3bf619, new int[] { 199, 5 } },
            { 0xda6132ff, new int[] { 330, 7 } },
            { 0xda746262, new int[] { 70, 7 } },
            { 0xda87b38c, new int[] { 2099, 6 } },
            { 0xda952297, new int[] { 249, 6 } },
            { 0xdab48923, new int[] { 209, 5 } },
            { 0xdac39ea9, new int[] { 13000, 5 } },
            { 0xdb240a0e, new int[] { 1739, 6 } },
            { 0xdb70dbc1, new int[] { 289, 1 } },
            { 0xdbeb6614, new int[] { 1500, 4 } },
            { 0xdc046a4c, new int[] { 153, 0 } },
            { 0xdc0b2d48, new int[] { 39, 5 } },
            { 0xdc0bef65, new int[] { 129, 11 } },
            { 0xdc2dd727, new int[] { 210, 5 } },
            { 0xdcb5a769, new int[] { 1239, 0 } },
            { 0xdce934ea, new int[] { 80, 5 } },
            { 0xdd2cd9be, new int[] { 239, 5 } },
            { 0xdd4e8362, new int[] { 2199, 3 } },
            { 0xddae91b4, new int[] { 229, 5 } },
            { 0xddbb7f1d, new int[] { 575, 0 } },
            { 0xddecd497, new int[] { 700, 6 } },
            { 0xddf205e3, new int[] { 23339, 6 } },
            { 0xde802250, new int[] { 325, 11 } },
            { 0xdec0fdda, new int[] { 125, 5 } },
            { 0xdf77ee12, new int[] { 25, 7 } },
            { 0xdf9adf21, new int[] { 180, 7 } },
            { 0xdfbb8259, new int[] { 4800, 2 } },
            { 0xdfd8f77a, new int[] { 150, 6 } },
            { 0xdfde21e4, new int[] { 399, 6 } },
            { 0xe075d96b, new int[] { 1200, 6 } },
            { 0xe0df7c0d, new int[] { 78, 0 } },
            { 0xe0e2da61, new int[] { 520, 1 } },
            { 0xe0f3926b, new int[] { 649, 1 } },
            { 0xe0f69b8f, new int[] { 899, 2 } },
            { 0xe112d26c, new int[] { 89, 6 } },
            { 0xe14bfa66, new int[] { 79, 1 } },
            { 0xe1577be5, new int[] { 215, 7 } },
            { 0xe163b26c, new int[] { 399, 1 } },
            { 0xe19c2cff, new int[] { 2999, 4 } },
            { 0xe1b42acd, new int[] { 375, 6 } },
            { 0xe209959f, new int[] { 3999, 3 } },
            { 0xe23f9020, new int[] { 75, 6 } },
            { 0xe4440b9b, new int[] { 310, 3 } },
            { 0xe445b4a6, new int[] { 3200, 4 } },
            { 0xe482787c, new int[] { 650, 11 } },
            { 0xe48328e1, new int[] { 250, 11 } },
            { 0xe48c2a23, new int[] { 200, 11 } },
            { 0xe48d0746, new int[] { 250, 11 } },
            { 0xe4a3dca2, new int[] { 19, 13 } },
            { 0xe4fb60eb, new int[] { 200, 11 } },
            { 0xe5293e72, new int[] { 160, 5 } },
            { 0xe53b581a, new int[] { 89, 7 } },
            { 0xe53fa2df, new int[] { 79, 7 } },
            { 0xe542c148, new int[] { 1600, 2 } },
            { 0xe56eb692, new int[] { 599, 8 } },
            { 0xe5c378c5, new int[] { 710, 1 } },
            { 0xe5d2ff1f, new int[] { 8999, 5 } },
            { 0xe60e754c, new int[] { 812, 6 } },
            { 0xe636aa91, new int[] { 159, 8 } },
            { 0xe6888167, new int[] { 239, 0 } },
            { 0xe69e35c0, new int[] { 5555, 2 } },
            { 0xe6e7f18b, new int[] { 199, 8 } },
            { 0xe6fb10f3, new int[] { 249, 13 } },
            { 0xe735b451, new int[] { 629, 7 } },
            { 0xe7938987, new int[] { 200, 0 } },
            { 0xe7d374d8, new int[] { 70, 5 } },
            { 0xe7d90c21, new int[] { 229, 9 } },
            { 0xe82917b6, new int[] { 299, 0 } },
            { 0xe89c0e48, new int[] { 16999, 6 } },
            { 0xe89ff41c, new int[] { 165, 13 } },
            { 0xe8db9140, new int[] { 131, 5 } },
            { 0xe8e88d38, new int[] { 7129, 3 } },
            { 0xe93c8663, new int[] { 899, 1 } },
            { 0xe94250e9, new int[] { 220, 0 } },
            { 0xe9693f98, new int[] { 159, 11 } },
            { 0xe999932c, new int[] { 142, 1 } },
            { 0xe9a90c22, new int[] { 199, 6 } },
            { 0xe9f11574, new int[] { 210, 0 } },
            { 0xe9fc1f39, new int[] { 1000, 10 } },
            { 0xea2f9b26, new int[] { 89, 3 } },
            { 0xea50c82f, new int[] { 55, 7 } },
            { 0xea71c9dc, new int[] { 672, 4 } },
            { 0xeac3d84f, new int[] { 300, 4 } },
            { 0xeb686709, new int[] { 400, 2 } },
            { 0xeb6c0a92, new int[] { 220, 1 } },
            { 0xeb945bc1, new int[] { 6800, 6 } },
            { 0xebbd5efc, new int[] { 254, 2 } },
            { 0xebdc8225, new int[] { 5421, 5 } },
            { 0xebe254b3, new int[] { 350, 4 } },
            { 0xec0f97f3, new int[] { 3785, 6 } },
            { 0xec3b09c0, new int[] { 230, 5 } },
            { 0xec86699b, new int[] { 65, 5 } },
            { 0xeca8d234, new int[] { 3110, 6 } },
            { 0xecadc9fd, new int[] { 0, 6 } },
            { 0xecf7dde6, new int[] { 70, 8 } },
            { 0xed8bea88, new int[] { 190, 0 } },
            { 0xede77ffb, new int[] { 551, 3 } },
            { 0xee3f527d, new int[] { 45, 7 } },
            { 0xee72ed45, new int[] { 85, 9 } },
            { 0xee8dc760, new int[] { 1787, 6 } },
            { 0xeea7d425, new int[] { 50, 3 } },
            { 0xeea95801, new int[] { 2336, 4 } },
            { 0xeebceb18, new int[] { 550, 2 } },
            { 0xeec5748a, new int[] { 95, 0 } },
            { 0xeeeb7597, new int[] { 875, 0 } },
            { 0xefb6a70c, new int[] { 300, 7 } },
            { 0xefba77ca, new int[] { 65, 5 } },
            { 0xefbdcfbf, new int[] { 389, 1 } },
            { 0xefc4c201, new int[] { 149, 5 } },
            { 0xeff1fdb0, new int[] { 3300, 5 } },
            { 0xf0ce033e, new int[] { 229, 13 } },
            { 0xf0d9ae7f, new int[] { 482, 6 } },
            { 0xf126ef7c, new int[] { 245, 6 } },
            { 0xf14f637a, new int[] { 480, 6 } },
            { 0xf157fd2a, new int[] { 26999, 6 } },
            { 0xf15b8010, new int[] { 649, 0 } },
            { 0xf191c159, new int[] { 1100, 7 } },
            { 0xf1f37c98, new int[] { 75, 1 } },
            { 0xf2010049, new int[] { 1900, 2 } },
            { 0xf219d380, new int[] { 2114, 2 } },
            { 0xf2999f51, new int[] { 190, 11 } },
            { 0xf2e97a59, new int[] { 173, 8 } },
            { 0xf3b82ddf, new int[] { 989, 1 } },
            { 0xf3ca84b4, new int[] { 87, 5 } },
            { 0xf3ccccbe, new int[] { 57, 5 } },
            { 0xf3f43ce9, new int[] { 599, 1 } },
            { 0xf3fab854, new int[] { 5200, 6 } },
            { 0xf4126cc4, new int[] { 8999, 6 } },
            { 0xf416b3ec, new int[] { 1333, 4 } },
            { 0xf4e5edc9, new int[] { 2349, 0 } },
            { 0xf538d3c7, new int[] { 65, 9 } },
            { 0xf53eaf26, new int[] { 175, 13 } },
            { 0xf56ce72c, new int[] { 299, 13 } },
            { 0xf5b89805, new int[] { 109, 0 } },
            { 0xf60db645, new int[] { 3699, 6 } },
            { 0xf6159793, new int[] { 125, 7 } },
            { 0xf61d92e8, new int[] { 489, 1 } },
            { 0xf662fbb4, new int[] { 980, 6 } },
            { 0xf718d902, new int[] { 710, 6 } },
            { 0xf73f0765, new int[] { 899, 6 } },
            { 0xf784f104, new int[] { 4999, 0 } },
            { 0xf78e916d, new int[] { 192, 6 } },
            { 0xf78ed967, new int[] { 376, 6 } },
            { 0xf78f37e6, new int[] { 55, 7 } },
            { 0xf79a89c2, new int[] { 250, 0 } },
            { 0xf7d7940c, new int[] { 1600, 5 } },
            { 0xf7f1f13c, new int[] { 95, 9 } },
            { 0xf86f37e4, new int[] { 399, 0 } },
            { 0xf88e6aac, new int[] { 53, 9 } },
            { 0xf8c07e5d, new int[] { 160, 8 } },
            { 0xf8e3fc32, new int[] { 0, 6 } },
            { 0xf8f818e1, new int[] { 2499, 10 } },
            { 0xf9ca6b20, new int[] { 27, 5 } },
            { 0xf9e11d1c, new int[] { 559, 11 } },
            { 0xfa733902, new int[] { 65, 11 } },
            { 0xfac41fac, new int[] { 2229, 5 } },
            { 0xfaf6c024, new int[] { 850, 0 } },
            { 0xfbc9619c, new int[] { 684, 5 } },
            { 0xfc55fe0a, new int[] { 225, 0 } },
            { 0xfc650ed2, new int[] { 828, 1 } },
            { 0xfc697613, new int[] { 0, 6 } },
            { 0xfc8d548b, new int[] { 3500, 3 } },
            { 0xfca517aa, new int[] { 228, 13 } },
            { 0xfd03db16, new int[] { 1250, 7 } },
            { 0xfd746ad2, new int[] { 180, 7 } },
            { 0xfd938797, new int[] { 160, 7 } },
            { 0xfddca51c, new int[] { 900, 6 } },
            { 0xfdef7fc2, new int[] { 795, 0 } },
            { 0xfe1a5de6, new int[] { 250, 5 } },
            { 0xfe7b8edb, new int[] { 438, 0 } },
            { 0xfe7c3fec, new int[] { 100, 7 } },
            { 0xfea08f34, new int[] { 4800, 2 } },
            { 0xfeac70dc, new int[] { 190, 0 } },
            { 0xfee30120, new int[] { 111, 5 } },
            { 0xfefda8d6, new int[] { 4900, 2 } },
            { 0xff7d2ca4, new int[] { 285, 7 } },
            { 0xff97ba8f, new int[] { 45, 5 } },
            { 0xff9f18e0, new int[] { 1000, 2 } },
            { 0xffbb3b82, new int[] { 179, 1 } },
            { 0xffbe1d84, new int[] { 99, 6 } },
            { 0xffc0ea58, new int[] { 799, 6 } }
        };

        public static int Eligible = 1114;
        public static long Checksum = 3356090364479856L;

        // Round 47 'catalog' SORT-BYTE IFF-LITERAL canon: generated by tools/iff_objd_canon.py over
        // game-data/The Sims (1114 eligible GUIDs) - mirror of FSO OBJD.Read version gating:
        // RoomSort=(byte)RoomFlags@37, Subs=(byte)FunctionSubsort@92, DT=(byte)DTSubsort@93,
        // Vac=(byte)VacationSubsort@95, Comm=(byte)CommunitySubsort@97, ST=(byte)STSubsort@101,
        // MT=(byte)MTSubsort@102 (IFF-literal: the v138 corpus carries 106 fields and the Round-47
        // engine fix parses them all - canon == served == raw IFF bytes, nothing left unread).
        public static readonly Dictionary<uint, int[]> CatalogSorts = new Dictionary<uint, int[]>{
            { 0x000001ca, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x000c147f, new int[] { 16, 128, 0, 0, 0, 0, 0 } },
            { 0x00230edc, new int[] { 2, 1, 0, 4, 0, 0, 0 } },
            { 0x00c2b68f, new int[] { 32, 128, 1, 3, 1, 1, 1 } },
            { 0x00c8a5f1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x00dd7908, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x00ddfef9, new int[] { 4, 1, 0, 9, 0, 0, 0 } },
            { 0x00e22e1e, new int[] { 64, 4, 131, 131, 131, 135, 143 } },
            { 0x00e30dc2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x00fe9850, new int[] { 0, 128, 2, 2, 2, 2, 0 } },
            { 0x01259297, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0x0125da9d, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0x0133d442, new int[] { 32, 2, 0, 1, 0, 135, 131 } },
            { 0x01f02564, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0x029c9043, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0x02f04067, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0x031db391, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x048567b7, new int[] { 32, 4, 3, 3, 3, 3, 130 } },
            { 0x04d86d1f, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x05777d82, new int[] { 8, 32, 130, 130, 130, 0, 0 } },
            { 0x05d17306, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x05dac986, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x068ead71, new int[] { 32, 16, 0, 0, 0, 0, 0 } },
            { 0x0768a9cb, new int[] { 8, 4, 128, 1, 128, 4, 128 } },
            { 0x09196e73, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0924bc1f, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0x099a621e, new int[] { 16, 128, 4, 12, 4, 1, 137 } },
            { 0x09cc9d1c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x09d8dcec, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0a02f059, new int[] { 16, 4, 143, 143, 143, 131, 143 } },
            { 0x0a717442, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0a8178e9, new int[] { 1, 4, 131, 11, 131, 131, 135 } },
            { 0x0a87e8cf, new int[] { 8, 4, 130, 3, 130, 134, 132 } },
            { 0x0ae5df54, new int[] { 16, 1, 4, 132, 4, 12, 8 } },
            { 0x0aec8c97, new int[] { 32, 2, 1, 1, 1, 1, 1 } },
            { 0x0afae8b0, new int[] { 0, 4, 131, 135, 131, 131, 3 } },
            { 0x0b1ad6cc, new int[] { 0, 0, 11, 8, 11, 131, 143 } },
            { 0x0b36e878, new int[] { 64, 1, 143, 131, 143, 135, 143 } },
            { 0x0bfdfb2a, new int[] { 0, 128, 1, 1, 1, 0, 0 } },
            { 0x0c04ca02, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0c0d4574, new int[] { 0, 128, 131, 130, 130, 130, 135 } },
            { 0x0c189d36, new int[] { 64, 1, 143, 131, 143, 135, 143 } },
            { 0x0ce4a17c, new int[] { 64, 2, 131, 131, 131, 135, 131 } },
            { 0x0d14b885, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0d192399, new int[] { 8, 128, 0, 1, 0, 4, 0 } },
            { 0x0d2dde73, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0d4ce6fd, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0d94642f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0dfd8714, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x0e4f2e14, new int[] { 0, 4, 131, 135, 131, 131, 131 } },
            { 0x0e82c943, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x0eb692fb, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0ecbec1c, new int[] { 0, 128, 11, 11, 11, 3, 131 } },
            { 0x0ede95bb, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0f1aa416, new int[] { 0, 128, 11, 8, 11, 128, 143 } },
            { 0x0f77759b, new int[] { 64, 4, 131, 131, 0, 0, 0 } },
            { 0x0f8beda7, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0x0fbb8bf8, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x0fd4ccef, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x0fe5fa64, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0x0ffc192a, new int[] { 16, 128, 12, 136, 12, 12, 136 } },
            { 0x10444309, new int[] { 0, 0, 143, 131, 143, 0, 0 } },
            { 0x108262be, new int[] { 64, 4, 131, 131, 128, 135, 131 } },
            { 0x110cf4b1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x112039a0, new int[] { 8, 4, 128, 0, 0, 0, 128 } },
            { 0x1181ccb1, new int[] { 64, 128, 143, 131, 143, 0, 0 } },
            { 0x11a92d74, new int[] { 2, 1, 0, 0, 0, 0, 0 } },
            { 0x11ec66d9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x12a45904, new int[] { 16, 128, 11, 139, 11, 3, 139 } },
            { 0x12d9e6a3, new int[] { 32, 1, 3, 3, 3, 7, 3 } },
            { 0x135e770f, new int[] { 128, 128, 131, 131, 131, 131, 131 } },
            { 0x14284046, new int[] { 0, 0, 131, 131, 131, 135, 131 } },
            { 0x144a7815, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0x144c3f7c, new int[] { 0, 0, 131, 131, 131, 135, 131 } },
            { 0x1468c3a7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x14ae1e58, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x14ef6729, new int[] { 0, 1, 132, 132, 132, 0, 128 } },
            { 0x151d17ed, new int[] { 32, 2, 130, 131, 131, 131, 131 } },
            { 0x15316867, new int[] { 64, 8, 131, 3, 131, 143, 135 } },
            { 0x15445522, new int[] { 64, 8, 131, 3, 131, 143, 135 } },
            { 0x154522af, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x15460fca, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x15597b59, new int[] { 64, 2, 142, 140, 142, 130, 143 } },
            { 0x157021de, new int[] { 0, 128, 142, 134, 142, 130, 143 } },
            { 0x15717e80, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1584c383, new int[] { 32, 1, 3, 3, 3, 7, 135 } },
            { 0x160bb3d1, new int[] { 2, 8, 0, 1, 0, 2, 0 } },
            { 0x165cd872, new int[] { 32, 32, 0, 1, 3, 0, 1 } },
            { 0x1671a218, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1694348f, new int[] { 0, 128, 2, 2, 2, 0, 0 } },
            { 0x16aed8d8, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x16d3a20d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x16f14e74, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x171527eb, new int[] { 0, 0, 131, 131, 131, 135, 131 } },
            { 0x17332210, new int[] { 0, 0, 143, 131, 143, 0, 0 } },
            { 0x17579980, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x1772dd4f, new int[] { 16, 8, 0, 133, 0, 12, 0 } },
            { 0x17f06fe1, new int[] { 0, 0, 131, 131, 131, 135, 131 } },
            { 0x17fc774d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x17fd98f7, new int[] { 2, 8, 0, 1, 0, 2, 0 } },
            { 0x182e7c24, new int[] { 8, 32, 0, 0, 0, 0, 0 } },
            { 0x19057c09, new int[] { 64, 2, 131, 131, 128, 132, 135 } },
            { 0x191b9479, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x1ab85e45, new int[] { 0, 1, 132, 132, 132, 0, 0 } },
            { 0x1ad5b840, new int[] { 32, 2, 1, 1, 1, 1, 1 } },
            { 0x1b490662, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x1b833d24, new int[] { 8, 2, 131, 5, 131, 135, 131 } },
            { 0x1bb40a4e, new int[] { 0, 128, 0, 132, 132, 0, 8 } },
            { 0x1bbdb732, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0x1c6b6d6a, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x1cc44f98, new int[] { 32, 2, 5, 1, 5, 1, 137 } },
            { 0x1d772052, new int[] { 0, 128, 0, 0, 0, 4, 0 } },
            { 0x1d8e6e89, new int[] { 8, 4, 130, 3, 130, 134, 132 } },
            { 0x1de172c2, new int[] { 128, 4, 131, 131, 131, 135, 135 } },
            { 0x1dff3cab, new int[] { 4, 1, 131, 3, 131, 143, 131 } },
            { 0x1e6d621b, new int[] { 64, 2, 143, 131, 143, 131, 131 } },
            { 0x1e743c39, new int[] { 0, 0, 0, 0, 132, 0, 0 } },
            { 0x1e8b9ea7, new int[] { 64, 1, 131, 132, 131, 131, 128 } },
            { 0x1e8dd05c, new int[] { 64, 2, 143, 131, 143, 140, 139 } },
            { 0x1f15af7e, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x1f287b65, new int[] { 0, 128, 2, 2, 2, 0, 2 } },
            { 0x1fac199d, new int[] { 1, 8, 1, 9, 0, 0, 0 } },
            { 0x2077e7de, new int[] { 0, 128, 0, 0, 0, 0, 2 } },
            { 0x2130c4e5, new int[] { 0, 1, 0, 0, 0, 0, 4 } },
            { 0x21332b58, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x21e65bbf, new int[] { 64, 128, 11, 8, 11, 128, 143 } },
            { 0x21f954a6, new int[] { 8, 4, 0, 0, 0, 4, 0 } },
            { 0x2256ba70, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2263dceb, new int[] { 16, 128, 8, 136, 8, 132, 8 } },
            { 0x228e96d2, new int[] { 16, 1, 7, 12, 7, 10, 2 } },
            { 0x22920f55, new int[] { 8, 2, 2, 3, 2, 135, 135 } },
            { 0x22dd513c, new int[] { 0, 0, 11, 0, 11, 0, 0 } },
            { 0x23941850, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x243f0fa0, new int[] { 0, 128, 0, 141, 0, 0, 0 } },
            { 0x246f6a23, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x24dad5c4, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0x2521000e, new int[] { 0, 128, 2, 0, 2, 0, 2 } },
            { 0x253c1a5e, new int[] { 16, 1, 4, 12, 4, 0, 0 } },
            { 0x254233ad, new int[] { 8, 4, 128, 1, 128, 136, 128 } },
            { 0x25b82d98, new int[] { 16, 1, 4, 7, 4, 128, 143 } },
            { 0x25ba0090, new int[] { 32, 2, 130, 130, 130, 130, 143 } },
            { 0x2644d3c7, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0x26ad0662, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x26ad56ff, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x26bfbb29, new int[] { 32, 1, 3, 3, 3, 7, 135 } },
            { 0x26d24804, new int[] { 0, 128, 2, 0, 2, 0, 2 } },
            { 0x26e91ef5, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x2768de7d, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x2796b726, new int[] { 32, 1, 3, 7, 3, 3, 131 } },
            { 0x27f8f51f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x28975923, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x28a0403e, new int[] { 64, 2, 135, 131, 135, 135, 143 } },
            { 0x2935d383, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x294439e2, new int[] { 32, 2, 0, 1, 0, 1, 133 } },
            { 0x294a48ee, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x29cb5a51, new int[] { 8, 4, 131, 135, 131, 135, 131 } },
            { 0x29eaef1f, new int[] { 64, 4, 131, 131, 131, 131, 131 } },
            { 0x2a262d32, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2a3d8714, new int[] { 1, 2, 0, 0, 0, 0, 0 } },
            { 0x2a7994bf, new int[] { 64, 2, 131, 131, 131, 139, 131 } },
            { 0x2a7d9829, new int[] { 16, 128, 12, 136, 12, 12, 140 } },
            { 0x2ab64a1d, new int[] { 1, 1, 3, 3, 3, 11, 3 } },
            { 0x2abc6e73, new int[] { 0, 0, 143, 131, 143, 0, 0 } },
            { 0x2aca7211, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2b4502a2, new int[] { 2, 32, 0, 1, 2, 0, 0 } },
            { 0x2b49a3f4, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0x2b83b971, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2b89fd8a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2b956ab9, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0x2ba019c7, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x2baa0029, new int[] { 64, 64, 0, 0, 0, 0, 140 } },
            { 0x2bb9b580, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2bb9e51d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2bd867fe, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0x2be7cbba, new int[] { 16, 128, 4, 140, 135, 131, 139 } },
            { 0x2c501364, new int[] { 64, 2, 131, 131, 131, 135, 135 } },
            { 0x2c555ea3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2c57d9f1, new int[] { 4, 128, 131, 139, 131, 131, 135 } },
            { 0x2c5ac343, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x2c7dff0e, new int[] { 4, 2, 0, 1, 0, 8, 0 } },
            { 0x2cfb155a, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x2d0a3de1, new int[] { 0, 128, 1, 1, 1, 0, 0 } },
            { 0x2d5f0d6c, new int[] { 0, 128, 1, 0, 0, 0, 0 } },
            { 0x2d773bf9, new int[] { 64, 4, 131, 131, 131, 135, 135 } },
            { 0x2e10faec, new int[] { 2, 8, 2, 11, 2, 14, 2 } },
            { 0x2e453ea5, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2e7f37ea, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x2e9fdca5, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0x2ecdb068, new int[] { 4, 32, 0, 137, 130, 0, 0 } },
            { 0x2f014f68, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2f42fc26, new int[] { 64, 2, 131, 131, 131, 135, 131 } },
            { 0x2f86506b, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x2f8715d1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2f92cd8f, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x2fc581ad, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x2fcd8a30, new int[] { 32, 1, 135, 143, 135, 135, 131 } },
            { 0x2fdf80dd, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x2ffaa585, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x3007996e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3026a0f4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x307ddfa6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3089d8d2, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x30bcda26, new int[] { 1, 4, 0, 1, 0, 0, 0 } },
            { 0x30f4b933, new int[] { 0, 128, 0, 0, 0, 0, 4 } },
            { 0x31a0e42c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x31edeb80, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0x3288ed87, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x328ed9e1, new int[] { 8, 4, 128, 1, 128, 4, 128 } },
            { 0x32f5a529, new int[] { 8, 128, 2, 131, 2, 140, 130 } },
            { 0x3313d86b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x334f732f, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x342a5e00, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0x34431396, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3480731c, new int[] { 1, 1, 3, 3, 3, 3, 0 } },
            { 0x3484c1f7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x34acf253, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x34f7be98, new int[] { 8, 2, 2, 3, 2, 135, 0 } },
            { 0x34fe4e17, new int[] { 4, 4, 131, 11, 131, 143, 131 } },
            { 0x354a1785, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x359e63bd, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x35b26d7f, new int[] { 64, 4, 0, 0, 0, 0, 0 } },
            { 0x35eaebdc, new int[] { 2, 8, 0, 0, 0, 0, 0 } },
            { 0x36113a6d, new int[] { 8, 128, 129, 129, 129, 129, 0 } },
            { 0x3658d1aa, new int[] { 1, 1, 3, 3, 3, 0, 3 } },
            { 0x3659dca2, new int[] { 16, 2, 4, 131, 4, 0, 0 } },
            { 0x3672a8b2, new int[] { 32, 128, 0, 1, 0, 1, 1 } },
            { 0x36f33011, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x36fe2161, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x37186734, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x371e3b21, new int[] { 32, 1, 1, 3, 3, 11, 139 } },
            { 0x37326795, new int[] { 0, 128, 132, 132, 132, 128, 143 } },
            { 0x3742732c, new int[] { 128, 16, 0, 0, 0, 0, 0 } },
            { 0x375dd48b, new int[] { 64, 2, 131, 131, 131, 131, 135 } },
            { 0x3794bcc5, new int[] { 4, 4, 131, 11, 131, 143, 143 } },
            { 0x37bfa925, new int[] { 64, 2, 143, 131, 143, 139, 131 } },
            { 0x38232e50, new int[] { 8, 4, 128, 129, 129, 129, 130 } },
            { 0x384c487b, new int[] { 64, 128, 131, 131, 131, 142, 131 } },
            { 0x391e258e, new int[] { 32, 2, 0, 1, 0, 1, 1 } },
            { 0x39262c4c, new int[] { 64, 4, 131, 131, 131, 131, 131 } },
            { 0x39319021, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x398cc7a0, new int[] { 128, 4, 128, 128, 128, 132, 131 } },
            { 0x39ce4936, new int[] { 16, 128, 143, 131, 143, 143, 143 } },
            { 0x39dd7192, new int[] { 64, 2, 130, 131, 130, 142, 130 } },
            { 0x39ed3b5a, new int[] { 64, 2, 130, 131, 130, 134, 130 } },
            { 0x3aa14a49, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0x3ac7f3be, new int[] { 8, 32, 130, 130, 130, 0, 0 } },
            { 0x3b0971f3, new int[] { 8, 4, 131, 135, 131, 134, 131 } },
            { 0x3bc18581, new int[] { 16, 128, 140, 128, 140, 128, 136 } },
            { 0x3c30cb5d, new int[] { 16, 128, 4, 4, 4, 128, 136 } },
            { 0x3c52b66d, new int[] { 64, 128, 143, 131, 143, 0, 131 } },
            { 0x3c566968, new int[] { 4, 1, 0, 9, 131, 143, 135 } },
            { 0x3c71d2e9, new int[] { 8, 4, 128, 129, 128, 128, 128 } },
            { 0x3c9ea694, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3d1f6466, new int[] { 64, 2, 130, 128, 128, 132, 128 } },
            { 0x3d3e7889, new int[] { 0, 128, 0, 0, 2, 0, 0 } },
            { 0x3dbfc043, new int[] { 64, 1, 131, 131, 131, 0, 0 } },
            { 0x3dcc1abe, new int[] { 8, 4, 128, 0, 0, 4, 128 } },
            { 0x3dea8b15, new int[] { 0, 0, 0, 1, 0, 0, 0 } },
            { 0x3def46a0, new int[] { 0, 0, 0, 1, 0, 0, 0 } },
            { 0x3e69a01d, new int[] { 16, 2, 2, 3, 2, 2, 2 } },
            { 0x3e6fda2f, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0x4008dbb7, new int[] { 2, 8, 2, 11, 2, 14, 2 } },
            { 0x404ee381, new int[] { 1, 1, 1, 0, 0, 0, 0 } },
            { 0x405046ff, new int[] { 16, 1, 0, 4, 0, 0, 0 } },
            { 0x40c778fd, new int[] { 0, 1, 132, 132, 132, 128, 136 } },
            { 0x4126d70f, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x417e691c, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x4188ca2b, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x41a50757, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x41c753c1, new int[] { 16, 128, 143, 143, 143, 131, 143 } },
            { 0x41e1f6f3, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0x41fdd8d8, new int[] { 8, 2, 2, 3, 2, 6, 134 } },
            { 0x4208b8e3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x42197abc, new int[] { 128, 2, 128, 1, 128, 0, 128 } },
            { 0x42316f67, new int[] { 32, 1, 3, 3, 3, 5, 1 } },
            { 0x42bcc380, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x42c078b3, new int[] { 1, 2, 1, 0, 1, 0, 0 } },
            { 0x431d46c8, new int[] { 32, 1, 3, 3, 131, 131, 139 } },
            { 0x4371fde9, new int[] { 64, 128, 131, 131, 131, 131, 131 } },
            { 0x43a116b4, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x43a15ebe, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x43b62717, new int[] { 16, 2, 132, 131, 132, 128, 138 } },
            { 0x440087f3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4450e4e0, new int[] { 0, 128, 0, 0, 0, 4, 0 } },
            { 0x44e8992a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4596ee35, new int[] { 2, 1, 0, 0, 0, 0, 0 } },
            { 0x45caf478, new int[] { 0, 128, 4, 12, 4, 1, 1 } },
            { 0x4699596d, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0x46ddd450, new int[] { 4, 4, 131, 11, 131, 139, 131 } },
            { 0x46e807b4, new int[] { 64, 8, 131, 131, 131, 131, 135 } },
            { 0x46ef5577, new int[] { 64, 8, 131, 131, 131, 135, 135 } },
            { 0x46f4e80b, new int[] { 8, 4, 0, 0, 0, 132, 0 } },
            { 0x471a0bf6, new int[] { 64, 1, 131, 131, 131, 134, 131 } },
            { 0x473532d6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x47a99575, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x47e917c4, new int[] { 1, 1, 3, 3, 3, 143, 131 } },
            { 0x47f39f51, new int[] { 8, 1, 131, 7, 129, 132, 128 } },
            { 0x481a74ec, new int[] { 64, 1, 131, 135, 131, 128, 128 } },
            { 0x48a153ad, new int[] { 128, 4, 128, 128, 128, 132, 131 } },
            { 0x49402e4f, new int[] { 16, 128, 143, 131, 143, 143, 143 } },
            { 0x497a0c3e, new int[] { 0, 0, 0, 1, 0, 0, 0 } },
            { 0x49f9c16f, new int[] { 128, 16, 0, 0, 0, 0, 0 } },
            { 0x4a21d3a7, new int[] { 32, 2, 0, 1, 0, 1, 1 } },
            { 0x4a459a5d, new int[] { 64, 4, 131, 131, 131, 135, 143 } },
            { 0x4a4d1966, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4a70df92, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x4a8ae8fb, new int[] { 32, 2, 1, 1, 1, 1, 1 } },
            { 0x4ae4f77c, new int[] { 64, 2, 143, 131, 143, 131, 135 } },
            { 0x4af2067e, new int[] { 16, 128, 143, 131, 143, 143, 143 } },
            { 0x4b4d30a9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4b69ea5a, new int[] { 64, 2, 143, 131, 143, 139, 131 } },
            { 0x4b78bf0e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4bd0b785, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4be4809a, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0x4c103662, new int[] { 4, 2, 0, 1, 0, 8, 0 } },
            { 0x4c442733, new int[] { 1, 1, 3, 3, 3, 3, 131 } },
            { 0x4c443fa4, new int[] { 1, 1, 3, 3, 3, 3, 131 } },
            { 0x4c446f39, new int[] { 1, 1, 3, 3, 3, 3, 131 } },
            { 0x4c659e7d, new int[] { 64, 128, 131, 131, 131, 131, 131 } },
            { 0x4c727c5d, new int[] { 1, 4, 131, 11, 131, 143, 131 } },
            { 0x4cd0523d, new int[] { 2, 8, 2, 11, 2, 140, 2 } },
            { 0x4d6da494, new int[] { 0, 128, 0, 0, 0, 4, 0 } },
            { 0x4ddf498c, new int[] { 64, 4, 0, 0, 0, 0, 0 } },
            { 0x4deefc68, new int[] { 0, 128, 1, 0, 0, 0, 0 } },
            { 0x4e1ea389, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0x4e665bf5, new int[] { 0, 128, 131, 130, 130, 130, 143 } },
            { 0x4ee8b3cf, new int[] { 4, 4, 131, 11, 131, 139, 135 } },
            { 0x4ef4ea16, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x4ef80f89, new int[] { 0, 128, 0, 132, 0, 0, 0 } },
            { 0x4f51d969, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4f8e1c8b, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0x4fa133c9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4fdec997, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x502367de, new int[] { 8, 2, 2, 3, 2, 2, 2 } },
            { 0x50b9bc6d, new int[] { 64, 2, 131, 131, 131, 135, 131 } },
            { 0x51474702, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5152a864, new int[] { 2, 32, 0, 1, 2, 0, 0 } },
            { 0x5156efbc, new int[] { 0, 1, 132, 132, 132, 0, 128 } },
            { 0x515e8635, new int[] { 32, 16, 0, 0, 0, 0, 0 } },
            { 0x5199e127, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x51a767e2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x51c23b85, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x52112235, new int[] { 64, 2, 143, 131, 143, 131, 131 } },
            { 0x5249b60e, new int[] { 2, 8, 2, 11, 2, 14, 2 } },
            { 0x5250b4c1, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x5295a7a8, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x52ae7a4e, new int[] { 4, 128, 128, 136, 128, 8, 0 } },
            { 0x52c81f9b, new int[] { 16, 128, 132, 136, 132, 0, 136 } },
            { 0x52ed4b7c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x52f6acd7, new int[] { 64, 1, 128, 128, 128, 8, 128 } },
            { 0x52f83bea, new int[] { 0, 0, 10, 10, 10, 0, 0 } },
            { 0x5359bfc1, new int[] { 64, 128, 143, 0, 143, 0, 136 } },
            { 0x536a3e4d, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x547f4934, new int[] { 0, 2, 2, 2, 2, 2, 2 } },
            { 0x54b3b81a, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x54c3e0cf, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x54c7f70d, new int[] { 64, 1, 130, 135, 130, 0, 128 } },
            { 0x552a8297, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0x55acb9e0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x55bcbc04, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0x56007c4b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x56276590, new int[] { 0, 128, 1, 1, 1, 0, 0 } },
            { 0x578f5050, new int[] { 0, 128, 0, 0, 0, 0, 2 } },
            { 0x57f68405, new int[] { 64, 128, 131, 131, 131, 135, 131 } },
            { 0x582f5936, new int[] { 4, 1, 0, 9, 0, 136, 143 } },
            { 0x5880d445, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0x58dde126, new int[] { 16, 8, 0, 133, 0, 0, 0 } },
            { 0x59132604, new int[] { 64, 2, 131, 131, 131, 139, 139 } },
            { 0x59133e93, new int[] { 64, 2, 143, 131, 143, 139, 139 } },
            { 0x594a89f0, new int[] { 0, 4, 131, 135, 131, 131, 131 } },
            { 0x59b200b5, new int[] { 64, 4, 131, 131, 131, 131, 131 } },
            { 0x59c389bc, new int[] { 128, 128, 2, 3, 2, 2, 2 } },
            { 0x5a01eb5b, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x5a12bc70, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5a1bc4bc, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x5a5cda09, new int[] { 32, 2, 0, 1, 0, 135, 131 } },
            { 0x5a6feced, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5a91cee0, new int[] { 0, 128, 128, 128, 128, 4, 128 } },
            { 0x5b048a52, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x5b088f5b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5b182c35, new int[] { 4, 1, 0, 9, 131, 143, 135 } },
            { 0x5b561459, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5bfb6140, new int[] { 16, 8, 0, 133, 0, 0, 0 } },
            { 0x5c198cbe, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5cb56b2d, new int[] { 16, 128, 4, 136, 4, 4, 8 } },
            { 0x5cdc712f, new int[] { 128, 2, 128, 1, 128, 0, 128 } },
            { 0x5d071f26, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0x5d0bbdb6, new int[] { 64, 1, 131, 131, 131, 131, 135 } },
            { 0x5d39291c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5d6b538a, new int[] { 0, 0, 0, 1, 0, 0, 0 } },
            { 0x5d723615, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5d767e1f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5d7b6688, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5dac929f, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x5e8b157a, new int[] { 64, 1, 131, 133, 131, 0, 128 } },
            { 0x5e9e42fc, new int[] { 8, 4, 128, 0, 0, 0, 0 } },
            { 0x5edb929d, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x5ede0bc4, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x5f421524, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5fa381c1, new int[] { 8, 2, 131, 5, 131, 131, 131 } },
            { 0x5faa302a, new int[] { 32, 16, 0, 0, 0, 0, 0 } },
            { 0x5fb93288, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x601b0c9a, new int[] { 128, 4, 131, 131, 131, 131, 131 } },
            { 0x6065ac2d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x60909779, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x60d99178, new int[] { 0, 128, 0, 141, 0, 0, 0 } },
            { 0x60e23da6, new int[] { 16, 128, 4, 12, 4, 4, 0 } },
            { 0x6106a1a7, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0x615c9169, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6188f5d7, new int[] { 0, 1, 132, 132, 132, 128, 136 } },
            { 0x61b7982e, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0x629be21f, new int[] { 1, 8, 0, 0, 0, 0, 0 } },
            { 0x62b6f3c1, new int[] { 128, 128, 131, 3, 128, 128, 135 } },
            { 0x62ccb570, new int[] { 0, 128, 130, 130, 130, 130, 130 } },
            { 0x62f8a7e4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6350e96f, new int[] { 0, 128, 0, 0, 2, 0, 0 } },
            { 0x637692ea, new int[] { 0, 128, 2, 0, 2, 0, 2 } },
            { 0x63a3d7d4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x645365da, new int[] { 32, 1, 3, 3, 3, 135, 135 } },
            { 0x645542df, new int[] { 64, 1, 128, 132, 128, 136, 128 } },
            { 0x64983e4d, new int[] { 8, 32, 130, 138, 130, 0, 0 } },
            { 0x64a10860, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x64f0e2bc, new int[] { 64, 2, 132, 132, 132, 128, 143 } },
            { 0x65274a4f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x65396e67, new int[] { 16, 2, 140, 128, 140, 128, 136 } },
            { 0x6585d104, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0x6593d291, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x65cb152b, new int[] { 0, 2, 4, 12, 4, 0, 0 } },
            { 0x66025e42, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6610c43c, new int[] { 64, 8, 131, 131, 131, 143, 135 } },
            { 0x66131ba8, new int[] { 0, 128, 0, 1, 0, 0, 0 } },
            { 0x66237c5d, new int[] { 32, 1, 1, 3, 3, 135, 131 } },
            { 0x663eaffc, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x66548be0, new int[] { 8, 4, 128, 129, 128, 128, 130 } },
            { 0x66877bef, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x6691c3d8, new int[] { 0, 128, 0, 0, 0, 0, 2 } },
            { 0x67406e73, new int[] { 64, 2, 132, 132, 132, 128, 140 } },
            { 0x675c18af, new int[] { 1, 2, 1, 0, 1, 0, 0 } },
            { 0x678f09ff, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6854da61, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x68897ff8, new int[] { 1, 1, 0, 0, 0, 0, 0 } },
            { 0x68af92a3, new int[] { 0, 0, 0, 132, 132, 0, 136 } },
            { 0x68c11912, new int[] { 8, 128, 0, 1, 0, 4, 0 } },
            { 0x6923633a, new int[] { 0, 128, 130, 130, 130, 130, 2 } },
            { 0x6928408e, new int[] { 0, 128, 131, 131, 131, 130, 134 } },
            { 0x6954e030, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0x69bd37af, new int[] { 0, 16, 0, 0, 0, 0, 0 } },
            { 0x6a3889f8, new int[] { 32, 1, 3, 3, 3, 7, 135 } },
            { 0x6a53de3c, new int[] { 0, 128, 2, 2, 2, 0, 0 } },
            { 0x6a55e034, new int[] { 8, 128, 131, 131, 131, 131, 0 } },
            { 0x6a776e73, new int[] { 0, 1, 128, 132, 128, 128, 132 } },
            { 0x6a8594ad, new int[] { 64, 128, 128, 128, 128, 4, 128 } },
            { 0x6ad59dd3, new int[] { 8, 4, 130, 3, 130, 134, 130 } },
            { 0x6aeb3d99, new int[] { 0, 1, 132, 132, 132, 128, 136 } },
            { 0x6b264238, new int[] { 64, 8, 0, 0, 0, 0, 0 } },
            { 0x6bad868b, new int[] { 16, 128, 136, 136, 136, 128, 8 } },
            { 0x6bf6d402, new int[] { 32, 1, 131, 131, 131, 131, 131 } },
            { 0x6c1bb706, new int[] { 128, 16, 0, 0, 0, 0, 0 } },
            { 0x6c3bbc3e, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0x6c582f42, new int[] { 4, 128, 131, 139, 131, 143, 135 } },
            { 0x6c7caec2, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x6c86123e, new int[] { 64, 16, 131, 0, 0, 0, 0 } },
            { 0x6c8bd4a3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6c9f7100, new int[] { 64, 2, 128, 128, 128, 132, 128 } },
            { 0x6cb7f434, new int[] { 64, 4, 131, 131, 131, 135, 0 } },
            { 0x6cc079c2, new int[] { 128, 128, 2, 3, 2, 6, 2 } },
            { 0x6d96dcfa, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6da8b998, new int[] { 16, 1, 4, 12, 7, 135, 139 } },
            { 0x6dd88e6d, new int[] { 64, 8, 0, 0, 0, 0, 0 } },
            { 0x6e0ec37e, new int[] { 4, 2, 0, 1, 0, 12, 0 } },
            { 0x6e325f22, new int[] { 64, 1, 131, 131, 131, 143, 131 } },
            { 0x6f2bb8ed, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6f35df40, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6f93a84a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6fb00726, new int[] { 4, 4, 131, 11, 131, 139, 135 } },
            { 0x702851b4, new int[] { 64, 2, 131, 131, 131, 143, 139 } },
            { 0x70467b85, new int[] { 0, 128, 4, 132, 4, 132, 128 } },
            { 0x7051ee96, new int[] { 0, 128, 0, 1, 0, 0, 0 } },
            { 0x70760da9, new int[] { 64, 1, 128, 128, 128, 132, 131 } },
            { 0x70b0d5f2, new int[] { 0, 128, 2, 2, 2, 0, 2 } },
            { 0x70fc19c3, new int[] { 64, 8, 135, 3, 135, 143, 143 } },
            { 0x718ea9c7, new int[] { 64, 128, 131, 131, 131, 135, 131 } },
            { 0x722a0fbf, new int[] { 0, 1, 0, 132, 0, 0, 0 } },
            { 0x724ca405, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x7280cbfc, new int[] { 32, 1, 3, 3, 3, 3, 2 } },
            { 0x72a5cbe3, new int[] { 0, 0, 129, 131, 128, 128, 129 } },
            { 0x72bdbb7d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x72fa6739, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0x7318c9df, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x7387f3a2, new int[] { 1, 128, 5, 8, 5, 1, 1 } },
            { 0x738a419a, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x73d6180d, new int[] { 1, 1, 3, 3, 3, 143, 131 } },
            { 0x73eef299, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x74d93e95, new int[] { 32, 2, 1, 1, 1, 133, 129 } },
            { 0x74e7ffa9, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0x75ac188c, new int[] { 1, 4, 131, 11, 131, 143, 131 } },
            { 0x75b0b2b7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x765bcec0, new int[] { 1, 128, 0, 0, 0, 0, 0 } },
            { 0x7726e3a6, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x77d26881, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x77d429bb, new int[] { 64, 4, 131, 131, 131, 131, 131 } },
            { 0x77d9944d, new int[] { 32, 2, 1, 13, 1, 1, 1 } },
            { 0x7839c094, new int[] { 64, 1, 131, 131, 131, 8, 131 } },
            { 0x78d7ea4d, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x78edcd25, new int[] { 64, 8, 131, 3, 131, 143, 131 } },
            { 0x7939d97d, new int[] { 2, 4, 130, 3, 130, 130, 130 } },
            { 0x795e70b6, new int[] { 8, 2, 2, 3, 2, 6, 134 } },
            { 0x79e2bb28, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x79fa7e6b, new int[] { 128, 4, 131, 131, 131, 131, 131 } },
            { 0x7a56922f, new int[] { 8, 32, 0, 1, 2, 0, 0 } },
            { 0x7a8fdbf4, new int[] { 0, 1, 132, 132, 132, 0, 128 } },
            { 0x7ab03de4, new int[] { 128, 4, 131, 131, 131, 131, 135 } },
            { 0x7b21053a, new int[] { 64, 16, 0, 0, 0, 0, 0 } },
            { 0x7b6c8c63, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0x7bea0977, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x7bfdcc88, new int[] { 0, 128, 4, 140, 4, 0, 0 } },
            { 0x7c21272e, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x7c24a376, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7c5e5c6d, new int[] { 8, 2, 130, 129, 130, 130, 135 } },
            { 0x7cab54a2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7cb11019, new int[] { 16, 128, 4, 12, 4, 128, 128 } },
            { 0x7d2089b6, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x7d2995e4, new int[] { 64, 8, 131, 131, 131, 131, 135 } },
            { 0x7d4a0f76, new int[] { 16, 1, 4, 12, 4, 0, 8 } },
            { 0x7d9b1a47, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7e09517e, new int[] { 64, 4, 131, 131, 131, 131, 131 } },
            { 0x7e1f25b1, new int[] { 16, 1, 0, 132, 4, 0, 128 } },
            { 0x7e56b1be, new int[] { 64, 8, 131, 131, 131, 135, 135 } },
            { 0x7e853dea, new int[] { 8, 4, 131, 135, 131, 135, 128 } },
            { 0x7e97444a, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x7ed5d1b6, new int[] { 16, 128, 140, 137, 132, 128, 143 } },
            { 0x7ef99b7e, new int[] { 0, 1, 132, 132, 132, 128, 136 } },
            { 0x7f0abd26, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7f1ae4cf, new int[] { 0, 128, 138, 138, 142, 2, 130 } },
            { 0x7f283620, new int[] { 32, 2, 0, 1, 0, 1, 1 } },
            { 0x7f775eee, new int[] { 128, 2, 128, 129, 128, 128, 128 } },
            { 0x7fab4493, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0x7fbe5529, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0x7fd422a4, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0x7fdf0fc1, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0x801f7235, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0x80858fd1, new int[] { 16, 1, 4, 12, 4, 0, 0 } },
            { 0x81179ab3, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x813a5a6b, new int[] { 32, 16, 0, 0, 0, 0, 0 } },
            { 0x81a87bab, new int[] { 64, 128, 0, 0, 0, 4, 0 } },
            { 0x81b3a551, new int[] { 8, 32, 0, 1, 2, 0, 0 } },
            { 0x81c7a5e0, new int[] { 16, 128, 4, 12, 4, 4, 8 } },
            { 0x81d97262, new int[] { 8, 4, 0, 0, 0, 134, 128 } },
            { 0x823a9ee0, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0x82e04c5b, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x82e0f146, new int[] { 8, 2, 2, 3, 2, 135, 143 } },
            { 0x83023da6, new int[] { 4, 2, 0, 1, 0, 0, 0 } },
            { 0x835ba084, new int[] { 128, 2, 128, 128, 128, 128, 128 } },
            { 0x83e0af58, new int[] { 64, 128, 3, 11, 3, 15, 143 } },
            { 0x83eb896c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x849207af, new int[] { 1, 2, 1, 0, 0, 0, 0 } },
            { 0x84ceb10b, new int[] { 64, 16, 0, 0, 0, 0, 0 } },
            { 0x84e0774c, new int[] { 4, 1, 0, 9, 131, 143, 135 } },
            { 0x852df95d, new int[] { 4, 1, 0, 9, 0, 143, 143 } },
            { 0x855cde99, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0x8592f25b, new int[] { 8, 32, 130, 138, 130, 0, 0 } },
            { 0x85b424a2, new int[] { 0, 128, 0, 2, 0, 0, 0 } },
            { 0x85b46ca8, new int[] { 0, 128, 2, 2, 0, 0, 2 } },
            { 0x85e00942, new int[] { 4, 2, 0, 1, 0, 12, 0 } },
            { 0x85e02a40, new int[] { 4, 2, 0, 1, 0, 12, 0 } },
            { 0x85e4adbe, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0x85f1a085, new int[] { 1, 4, 0, 1, 0, 0, 0 } },
            { 0x8613a26a, new int[] { 0, 0, 0, 0, 140, 0, 0 } },
            { 0x862ccc31, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x86e0bbb5, new int[] { 64, 8, 0, 0, 0, 0, 0 } },
            { 0x877feca1, new int[] { 64, 2, 143, 131, 143, 131, 143 } },
            { 0x87c44d57, new int[] { 64, 128, 128, 134, 128, 132, 128 } },
            { 0x87c99127, new int[] { 1, 2, 1, 0, 0, 0, 0 } },
            { 0x87d00adc, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x880bb75e, new int[] { 16, 128, 0, 0, 0, 0, 0 } },
            { 0x88103f3e, new int[] { 64, 2, 130, 131, 130, 142, 136 } },
            { 0x882480a6, new int[] { 1, 1, 1, 0, 0, 0, 0 } },
            { 0x88417fac, new int[] { 128, 16, 0, 0, 0, 0, 0 } },
            { 0x8841dbb9, new int[] { 0, 1, 132, 132, 132, 0, 128 } },
            { 0x88734ce8, new int[] { 0, 128, 0, 0, 0, 4, 0 } },
            { 0x88b6a098, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x88ede461, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x89173b24, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x8942bdc7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8a712a85, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0x8ad987b8, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x8b04d5e1, new int[] { 0, 128, 8, 131, 8, 132, 9 } },
            { 0x8ba56284, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8bd70015, new int[] { 1, 1, 3, 3, 3, 135, 131 } },
            { 0x8bf2e852, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8c44ba58, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8c9350d2, new int[] { 16, 128, 4, 140, 4, 8, 136 } },
            { 0x8c9e33c9, new int[] { 16, 2, 0, 13, 0, 135, 143 } },
            { 0x8cb4d449, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8d1b6101, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8d4ee7df, new int[] { 64, 2, 131, 131, 131, 143, 138 } },
            { 0x8d70f7b3, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x8da9ffb4, new int[] { 0, 4, 131, 135, 131, 131, 3 } },
            { 0x8dab571f, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x8e3520bd, new int[] { 32, 128, 0, 1, 0, 1, 1 } },
            { 0x8e880484, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8e8d9ca9, new int[] { 0, 1, 128, 132, 128, 128, 132 } },
            { 0x8eaa68ad, new int[] { 0, 0, 0, 1, 0, 0, 0 } },
            { 0x8eb8e1a6, new int[] { 64, 8, 131, 3, 131, 11, 139 } },
            { 0x8ec5750d, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0x8eec19f7, new int[] { 32, 32, 0, 1, 3, 0, 1 } },
            { 0x8f01bdcb, new int[] { 8, 2, 2, 3, 2, 135, 130 } },
            { 0x8f033944, new int[] { 128, 128, 131, 131, 131, 135, 0 } },
            { 0x8f300ef1, new int[] { 16, 2, 4, 4, 4, 128, 0 } },
            { 0x8f667b23, new int[] { 64, 2, 131, 131, 131, 135, 131 } },
            { 0x8fb1bf00, new int[] { 16, 128, 143, 131, 143, 143, 143 } },
            { 0x8fca5a7b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8fed54c2, new int[] { 16, 8, 0, 133, 0, 12, 0 } },
            { 0x8ff0ca39, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x9082401f, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x90af09f4, new int[] { 64, 128, 131, 131, 131, 135, 131 } },
            { 0x90c1c7cb, new int[] { 128, 2, 4, 5, 4, 12, 136 } },
            { 0x90c7c9e4, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0x912cbed4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x91767a36, new int[] { 1, 1, 3, 3, 3, 143, 131 } },
            { 0x91ed6984, new int[] { 64, 1, 131, 131, 128, 128, 128 } },
            { 0x921fda07, new int[] { 32, 2, 0, 1, 0, 135, 131 } },
            { 0x9293b59e, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x92c87dc0, new int[] { 64, 4, 131, 131, 131, 135, 135 } },
            { 0x9367e97a, new int[] { 16, 128, 8, 136, 8, 128, 8 } },
            { 0x93f98c8a, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x94082f6d, new int[] { 0, 128, 0, 0, 0, 0, 4 } },
            { 0x948aed3c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x94aa32f4, new int[] { 128, 2, 128, 5, 128, 128, 136 } },
            { 0x94d2df9d, new int[] { 64, 2, 128, 128, 128, 132, 128 } },
            { 0x94f5cda8, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0x94fd43dc, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0x952bcac7, new int[] { 64, 8, 131, 3, 131, 11, 131 } },
            { 0x95624034, new int[] { 64, 1, 131, 131, 131, 135, 143 } },
            { 0x95a59fd2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x95b09639, new int[] { 0, 128, 2, 0, 2, 0, 2 } },
            { 0x95d4de33, new int[] { 0, 128, 2, 0, 2, 0, 2 } },
            { 0x95efe015, new int[] { 64, 2, 131, 131, 131, 131, 135 } },
            { 0x9667a489, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x969bde47, new int[] { 128, 4, 131, 131, 131, 139, 131 } },
            { 0x96a5928b, new int[] { 32, 128, 1, 1, 1, 1, 1 } },
            { 0x96c58db2, new int[] { 4, 4, 131, 11, 131, 143, 135 } },
            { 0x96cb67ae, new int[] { 16, 32, 0, 1, 2, 0, 0 } },
            { 0x96e3398d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x97a845c3, new int[] { 16, 1, 0, 132, 0, 0, 0 } },
            { 0x984671a9, new int[] { 64, 8, 131, 3, 131, 143, 143 } },
            { 0x98a0a6d6, new int[] { 64, 128, 131, 130, 130, 130, 131 } },
            { 0x98e09eb3, new int[] { 64, 2, 131, 131, 131, 135, 135 } },
            { 0x98e0f8bd, new int[] { 64, 128, 131, 3, 131, 132, 0 } },
            { 0x98e9cfe2, new int[] { 2, 8, 2, 11, 2, 8, 0 } },
            { 0x99120547, new int[] { 8, 2, 2, 3, 2, 135, 135 } },
            { 0x99400b4a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x994fb8f8, new int[] { 8, 2, 2, 3, 2, 2, 2 } },
            { 0x996de865, new int[] { 8, 2, 2, 3, 2, 2, 2 } },
            { 0x997beef4, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0x9985a06f, new int[] { 8, 2, 2, 3, 2, 6, 134 } },
            { 0x99e0a9b1, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0x9a0d8249, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9a37fba7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9aa2312f, new int[] { 0, 128, 2, 2, 2, 0, 0 } },
            { 0x9ae018bb, new int[] { 64, 8, 131, 3, 131, 143, 143 } },
            { 0x9b3340b9, new int[] { 0, 128, 2, 2, 2, 0, 0 } },
            { 0x9b3a5cdf, new int[] { 128, 2, 128, 1, 128, 0, 128 } },
            { 0x9b3cdd30, new int[] { 16, 128, 4, 1, 4, 128, 138 } },
            { 0x9be0e2bf, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x9c223167, new int[] { 64, 2, 128, 128, 128, 132, 128 } },
            { 0x9c541464, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9c59140e, new int[] { 128, 16, 0, 0, 0, 0, 0 } },
            { 0x9cb1bb9b, new int[] { 64, 1, 131, 132, 131, 0, 128 } },
            { 0x9d4b03a5, new int[] { 4, 8, 0, 9, 0, 0, 0 } },
            { 0x9da7b210, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9ede28ae, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9ee7eb60, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9f12673e, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0x9f3a38b9, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0x9f7295a9, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0x9f733d4f, new int[] { 0, 128, 0, 0, 3, 0, 3 } },
            { 0x9fa148dc, new int[] { 1, 4, 1, 0, 0, 0, 0 } },
            { 0x9fb223ce, new int[] { 128, 4, 0, 0, 0, 0, 0 } },
            { 0x9fd45e39, new int[] { 16, 128, 4, 136, 4, 142, 136 } },
            { 0xa01e500d, new int[] { 1, 8, 1, 9, 0, 0, 0 } },
            { 0xa022c70c, new int[] { 16, 128, 143, 8, 143, 131, 143 } },
            { 0xa02c8857, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa072951f, new int[] { 32, 2, 129, 129, 129, 129, 129 } },
            { 0xa08dd989, new int[] { 16, 128, 140, 136, 132, 132, 136 } },
            { 0xa08e0c14, new int[] { 16, 4, 128, 1, 128, 4, 128 } },
            { 0xa0af4e74, new int[] { 64, 2, 132, 128, 132, 128, 143 } },
            { 0xa0ee756f, new int[] { 64, 2, 130, 131, 130, 6, 130 } },
            { 0xa11d2875, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0xa13f39cf, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0xa1594e42, new int[] { 64, 1, 131, 131, 131, 135, 0 } },
            { 0xa1755ff7, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0xa1bd4c80, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xa1d5048a, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0xa1d6fe56, new int[] { 8, 128, 2, 3, 2, 132, 128 } },
            { 0xa22c5865, new int[] { 0, 128, 140, 136, 140, 128, 136 } },
            { 0xa2666952, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xa2daa08c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa2fe803a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa46aed33, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0xa478d407, new int[] { 64, 2, 143, 131, 143, 131, 131 } },
            { 0xa483d725, new int[] { 2, 16, 0, 0, 0, 0, 0 } },
            { 0xa489640d, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0xa4d8c299, new int[] { 16, 128, 4, 12, 4, 132, 136 } },
            { 0xa4e75cff, new int[] { 16, 128, 143, 131, 143, 135, 143 } },
            { 0xa5004e8c, new int[] { 64, 1, 128, 128, 128, 8, 128 } },
            { 0xa5b9515a, new int[] { 128, 128, 0, 0, 0, 4, 0 } },
            { 0xa5c0bb1e, new int[] { 128, 2, 132, 133, 132, 128, 136 } },
            { 0xa607cc98, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xa67ac823, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa6a4a1ac, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0xa6aebf5b, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0xa6caf1dd, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xa7340ec3, new int[] { 1, 4, 129, 137, 129, 137, 129 } },
            { 0xa77f36ae, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0xa7a70f83, new int[] { 0, 1, 0, 132, 0, 0, 0 } },
            { 0xa805e814, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0xa82ca08e, new int[] { 16, 128, 4, 136, 4, 140, 136 } },
            { 0xa8ad8714, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa8bdd25b, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xa8c59f83, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa90a7d01, new int[] { 128, 8, 2, 3, 2, 135, 0 } },
            { 0xa91403ab, new int[] { 128, 2, 128, 1, 128, 0, 128 } },
            { 0xa91d4963, new int[] { 128, 2, 128, 1, 128, 0, 128 } },
            { 0xa94c3259, new int[] { 32, 1, 135, 143, 135, 135, 131 } },
            { 0xa9c05832, new int[] { 8, 1, 131, 7, 129, 132, 128 } },
            { 0xa9ec8419, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0xaa2f9672, new int[] { 8, 4, 0, 0, 0, 4, 0 } },
            { 0xaa655e9f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xaa75a097, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xaa86ad2a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xaae98936, new int[] { 0, 128, 0, 0, 0, 4, 0 } },
            { 0xaaf447d6, new int[] { 128, 8, 2, 3, 2, 6, 130 } },
            { 0xab101956, new int[] { 32, 2, 5, 1, 5, 1, 9 } },
            { 0xab647747, new int[] { 64, 16, 0, 0, 0, 0, 0 } },
            { 0xabe586cd, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xabfc34d3, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0xabfc4be9, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0xac262843, new int[] { 64, 32, 130, 130, 130, 0, 130 } },
            { 0xac66d6ca, new int[] { 0, 0, 136, 136, 136, 0, 0 } },
            { 0xacee4ba5, new int[] { 64, 128, 131, 131, 131, 131, 131 } },
            { 0xad005811, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xad08fdc0, new int[] { 1, 4, 1, 0, 0, 0, 0 } },
            { 0xad0b9dd6, new int[] { 64, 128, 0, 0, 0, 0, 0 } },
            { 0xad12e0d0, new int[] { 64, 8, 131, 3, 131, 143, 131 } },
            { 0xad2c5018, new int[] { 0, 128, 131, 131, 131, 130, 134 } },
            { 0xad4cf878, new int[] { 0, 4, 131, 135, 131, 135, 131 } },
            { 0xad4d0d62, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0xad591aac, new int[] { 64, 128, 131, 3, 131, 131, 131 } },
            { 0xad67a694, new int[] { 0, 128, 138, 10, 138, 130, 130 } },
            { 0xad67be03, new int[] { 0, 128, 137, 0, 137, 129, 129 } },
            { 0xad67ee9e, new int[] { 0, 128, 137, 10, 137, 129, 129 } },
            { 0xae12c97a, new int[] { 1, 1, 3, 3, 3, 131, 131 } },
            { 0xae1daf72, new int[] { 16, 128, 4, 140, 4, 136, 136 } },
            { 0xae5c99e7, new int[] { 4, 1, 3, 3, 3, 139, 131 } },
            { 0xae6e43d6, new int[] { 64, 1, 131, 131, 131, 135, 143 } },
            { 0xaed36fcb, new int[] { 0, 128, 5, 8, 5, 1, 1 } },
            { 0xaed3c88a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xaf4a1300, new int[] { 64, 8, 131, 3, 131, 143, 143 } },
            { 0xaf85a57c, new int[] { 1, 8, 0, 0, 0, 0, 0 } },
            { 0xafabcc3d, new int[] { 128, 2, 128, 128, 128, 128, 128 } },
            { 0xb01c2620, new int[] { 0, 128, 143, 138, 143, 3, 130 } },
            { 0xb0706e2a, new int[] { 0, 128, 137, 136, 137, 1, 129 } },
            { 0xb1153e7d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb1230866, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0xb159c37d, new int[] { 128, 128, 131, 131, 131, 135, 131 } },
            { 0xb1b176bd, new int[] { 0, 128, 142, 138, 142, 0, 130 } },
            { 0xb21b9510, new int[] { 64, 4, 131, 131, 131, 130, 131 } },
            { 0xb26ab256, new int[] { 0, 32, 0, 4, 4, 0, 136 } },
            { 0xb31edff8, new int[] { 64, 1, 0, 0, 0, 132, 128 } },
            { 0xb346903c, new int[] { 64, 2, 131, 131, 131, 139, 131 } },
            { 0xb3760ba8, new int[] { 0, 128, 131, 131, 131, 130, 134 } },
            { 0xb407a783, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xb46b0e55, new int[] { 64, 1, 131, 131, 131, 143, 131 } },
            { 0xb48a1144, new int[] { 64, 2, 131, 131, 131, 131, 135 } },
            { 0xb499ec29, new int[] { 64, 1, 131, 4, 0, 0, 0 } },
            { 0xb4b333e4, new int[] { 64, 2, 143, 131, 143, 131, 143 } },
            { 0xb5215809, new int[] { 0, 0, 0, 1, 0, 0, 0 } },
            { 0xb5250c97, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0xb53903aa, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb56a0aff, new int[] { 8, 32, 130, 138, 130, 0, 0 } },
            { 0xb6236b64, new int[] { 8, 2, 2, 3, 2, 2, 2 } },
            { 0xb62454df, new int[] { 32, 16, 0, 0, 0, 0, 0 } },
            { 0xb668dd56, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb78e7f8d, new int[] { 128, 4, 131, 131, 0, 0, 0 } },
            { 0xb790a819, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb796426e, new int[] { 0, 128, 143, 138, 143, 131, 143 } },
            { 0xb797465f, new int[] { 16, 1, 0, 0, 0, 132, 128 } },
            { 0xb79f3182, new int[] { 128, 128, 131, 131, 131, 132, 131 } },
            { 0xb7cd406c, new int[] { 4, 128, 131, 139, 131, 143, 135 } },
            { 0xb7eaacba, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb80950cc, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0xb867117e, new int[] { 64, 1, 0, 4, 0, 0, 0 } },
            { 0xb87a8d3f, new int[] { 64, 4, 131, 131, 131, 135, 143 } },
            { 0xb89b2451, new int[] { 64, 128, 131, 131, 131, 135, 131 } },
            { 0xb9020120, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb94755df, new int[] { 1, 2, 1, 0, 1, 0, 0 } },
            { 0xb99d5c6e, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0xb9b0c862, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xba67ead2, new int[] { 16, 128, 143, 131, 143, 135, 139 } },
            { 0xba6e27bb, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xba71a336, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0xba7e8047, new int[] { 128, 16, 0, 0, 0, 0, 0 } },
            { 0xba9dc16a, new int[] { 16, 1, 4, 132, 4, 0, 0 } },
            { 0xbb0846ea, new int[] { 16, 128, 4, 136, 4, 142, 136 } },
            { 0xbb1bcf68, new int[] { 8, 4, 130, 3, 130, 134, 130 } },
            { 0xbb626ef3, new int[] { 1, 4, 1, 0, 0, 0, 0 } },
            { 0xbb83a08a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbba46c20, new int[] { 16, 128, 4, 131, 4, 0, 0 } },
            { 0xbc12cd61, new int[] { 16, 128, 11, 139, 11, 131, 139 } },
            { 0xbc3ba088, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xbc4b1c4f, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xbc4b7a23, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xbc537c52, new int[] { 16, 32, 0, 1, 2, 0, 0 } },
            { 0xbc661500, new int[] { 64, 8, 131, 3, 131, 143, 135 } },
            { 0xbcab486a, new int[] { 32, 1, 3, 3, 3, 135, 131 } },
            { 0xbcc81aff, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbd78c75d, new int[] { 0, 128, 0, 0, 0, 0, 143 } },
            { 0xbdcf6469, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbde052c5, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbe1d7003, new int[] { 16, 128, 140, 128, 140, 0, 136 } },
            { 0xbe773f58, new int[] { 0, 0, 11, 8, 11, 131, 143 } },
            { 0xbeb0f165, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbedd7b26, new int[] { 4, 32, 0, 1, 2, 0, 128 } },
            { 0xbf137195, new int[] { 1, 4, 0, 1, 0, 0, 0 } },
            { 0xbf1b918e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbf51b5f3, new int[] { 16, 128, 132, 128, 132, 128, 132 } },
            { 0xbfc58dd0, new int[] { 0, 1, 132, 132, 132, 0, 128 } },
            { 0xc02b406a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc041ed5b, new int[] { 2, 8, 131, 11, 131, 143, 0 } },
            { 0xc0e20936, new int[] { 0, 128, 128, 128, 128, 132, 128 } },
            { 0xc0e64804, new int[] { 16, 128, 4, 140, 4, 8, 136 } },
            { 0xc192dacc, new int[] { 8, 2, 131, 5, 131, 131, 131 } },
            { 0xc1b6da6a, new int[] { 32, 2, 0, 1, 0, 135, 131 } },
            { 0xc25fcf86, new int[] { 64, 4, 131, 131, 131, 135, 143 } },
            { 0xc26265b3, new int[] { 128, 4, 131, 131, 131, 135, 135 } },
            { 0xc2a91fd0, new int[] { 32, 128, 0, 0, 0, 0, 0 } },
            { 0xc2f1a050, new int[] { 32, 1, 3, 3, 3, 3, 3 } },
            { 0xc349c14c, new int[] { 0, 128, 0, 1, 0, 0, 0 } },
            { 0xc3927fa3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc3efd759, new int[] { 64, 128, 143, 139, 143, 0, 143 } },
            { 0xc4370e53, new int[] { 0, 128, 132, 132, 132, 0, 0 } },
            { 0xc4387388, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc48f31a0, new int[] { 8, 4, 128, 1, 128, 4, 134 } },
            { 0xc4c35536, new int[] { 128, 4, 131, 131, 131, 131, 131 } },
            { 0xc523d73d, new int[] { 0, 0, 140, 136, 140, 128, 136 } },
            { 0xc56562c1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc58b5c78, new int[] { 8, 4, 128, 1, 128, 4, 0 } },
            { 0xc5d8a768, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc606154b, new int[] { 64, 128, 0, 11, 131, 143, 143 } },
            { 0xc6448ae4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc69b6e73, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc6b2bdd5, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc71e43cd, new int[] { 0, 128, 131, 131, 131, 130, 134 } },
            { 0xc73c14a4, new int[] { 128, 4, 131, 131, 131, 131, 131 } },
            { 0xc74e7da7, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xc7a7215c, new int[] { 0, 128, 131, 131, 131, 131, 131 } },
            { 0xc7d177e1, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xc7d6cf27, new int[] { 64, 4, 135, 131, 135, 135, 139 } },
            { 0xc7efe216, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0xc884fc9e, new int[] { 64, 1, 131, 131, 131, 135, 135 } },
            { 0xc8ebdd42, new int[] { 8, 128, 0, 1, 0, 4, 0 } },
            { 0xc96b93eb, new int[] { 8, 16, 0, 0, 0, 0, 0 } },
            { 0xc99ac74e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xca528b1d, new int[] { 0, 128, 143, 138, 143, 3, 143 } },
            { 0xca55ed92, new int[] { 128, 4, 131, 131, 131, 133, 135 } },
            { 0xcaf38382, new int[] { 32, 1, 3, 3, 3, 3, 131 } },
            { 0xcb386e73, new int[] { 64, 4, 143, 131, 143, 135, 143 } },
            { 0xcb6e77ab, new int[] { 8, 4, 130, 3, 130, 134, 130 } },
            { 0xcb790e58, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcbc6c317, new int[] { 0, 0, 143, 138, 143, 3, 143 } },
            { 0xcbd2be4e, new int[] { 16, 128, 140, 136, 140, 128, 136 } },
            { 0xcbe53119, new int[] { 8, 2, 2, 3, 2, 2, 130 } },
            { 0xcbef938d, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0xcbeff420, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0xcc66ad61, new int[] { 8, 4, 128, 0, 0, 0, 0 } },
            { 0xcc944729, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0xcce4be4f, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0xcd39bb3a, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0xcda8d70a, new int[] { 32, 2, 1, 1, 1, 1, 1 } },
            { 0xcdd887b1, new int[] { 1, 2, 1, 0, 1, 0, 0 } },
            { 0xce10c801, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xce3615a5, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0xce47dd58, new int[] { 32, 16, 0, 0, 0, 0, 0 } },
            { 0xce4b38cf, new int[] { 64, 128, 139, 131, 139, 131, 0 } },
            { 0xce4d6011, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xce6c2041, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xce6c4713, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xceb2f9b8, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcf000516, new int[] { 32, 2, 4, 13, 4, 129, 129 } },
            { 0xcf18067e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcf5a1a2f, new int[] { 0, 64, 0, 0, 0, 0, 4 } },
            { 0xcf5b61d3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcf62b688, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0xcf89e47b, new int[] { 0, 128, 0, 0, 0, 0, 143 } },
            { 0xcfd10860, new int[] { 0, 128, 143, 143, 143, 143, 143 } },
            { 0xcfe94d2c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd0a0f269, new int[] { 64, 64, 0, 0, 0, 0, 4 } },
            { 0xd0a244e4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd0b754e0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd1180b2a, new int[] { 32, 1, 3, 15, 3, 135, 131 } },
            { 0xd158b046, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd191aff7, new int[] { 128, 2, 128, 128, 128, 128, 128 } },
            { 0xd191ff6a, new int[] { 128, 2, 128, 128, 128, 128, 128 } },
            { 0xd1bc4074, new int[] { 4, 128, 131, 139, 131, 143, 135 } },
            { 0xd1ef8174, new int[] { 16, 16, 0, 0, 0, 0, 0 } },
            { 0xd2944a33, new int[] { 8, 128, 2, 3, 2, 2, 139 } },
            { 0xd2a03993, new int[] { 64, 1, 0, 0, 0, 0, 0 } },
            { 0xd2bb9953, new int[] { 128, 1, 0, 0, 0, 0, 0 } },
            { 0xd389c839, new int[] { 64, 128, 131, 11, 131, 143, 143 } },
            { 0xd3a28b82, new int[] { 32, 2, 1, 1, 1, 129, 129 } },
            { 0xd4588282, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0xd45e4bea, new int[] { 0, 128, 142, 138, 142, 139, 139 } },
            { 0xd4709f75, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd49346ff, new int[] { 8, 4, 0, 0, 0, 4, 0 } },
            { 0xd4bf28f3, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xd5a34f0f, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0xd5ca5425, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd608b270, new int[] { 0, 128, 8, 8, 8, 132, 136 } },
            { 0xd68e73d8, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd735a326, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd73d1f82, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xd7404661, new int[] { 8, 32, 0, 1, 2, 0, 0 } },
            { 0xd84d39ce, new int[] { 32, 2, 0, 1, 0, 1, 3 } },
            { 0xd8bb3aa8, new int[] { 8, 128, 2, 131, 2, 131, 0 } },
            { 0xd95f52ef, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0xd97681db, new int[] { 32, 1, 3, 3, 3, 7, 135 } },
            { 0xd985a7ad, new int[] { 0, 128, 2, 2, 2, 0, 2 } },
            { 0xd9a73952, new int[] { 128, 8, 2, 3, 2, 135, 0 } },
            { 0xd9ae1ae5, new int[] { 2, 4, 130, 3, 130, 130, 130 } },
            { 0xd9bd21cd, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd9e3bf7b, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xda2b8f79, new int[] { 32, 128, 0, 0, 0, 0, 0 } },
            { 0xda3bf619, new int[] { 0, 1, 131, 131, 131, 135, 143 } },
            { 0xda6132ff, new int[] { 64, 2, 131, 131, 131, 135, 143 } },
            { 0xda746262, new int[] { 16, 128, 131, 139, 131, 131, 143 } },
            { 0xda87b38c, new int[] { 64, 128, 0, 0, 0, 0, 0 } },
            { 0xda952297, new int[] { 0, 0, 11, 8, 11, 131, 143 } },
            { 0xdab48923, new int[] { 0, 1, 131, 131, 131, 135, 143 } },
            { 0xdac39ea9, new int[] { 64, 2, 143, 131, 143, 131, 143 } },
            { 0xdb240a0e, new int[] { 0, 128, 2, 2, 2, 0, 0 } },
            { 0xdb70dbc1, new int[] { 32, 2, 1, 1, 1, 133, 129 } },
            { 0xdbeb6614, new int[] { 4, 2, 0, 1, 0, 12, 0 } },
            { 0xdc046a4c, new int[] { 8, 128, 2, 3, 2, 6, 2 } },
            { 0xdc0b2d48, new int[] { 64, 1, 131, 131, 131, 135, 143 } },
            { 0xdc0bef65, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xdc2dd727, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xdcb5a769, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xdce934ea, new int[] { 16, 128, 140, 137, 140, 128, 136 } },
            { 0xdd2cd9be, new int[] { 0, 1, 131, 131, 131, 135, 143 } },
            { 0xdd4e8362, new int[] { 64, 1, 130, 135, 130, 0, 128 } },
            { 0xddae91b4, new int[] { 0, 1, 131, 131, 131, 135, 143 } },
            { 0xddbb7f1d, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0xddecd497, new int[] { 64, 16, 0, 0, 0, 0, 0 } },
            { 0xddf205e3, new int[] { 0, 128, 0, 0, 0, 0, 4 } },
            { 0xde802250, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xdec0fdda, new int[] { 16, 0, 132, 128, 132, 0, 136 } },
            { 0xdf77ee12, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0xdf9adf21, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0xdfbb8259, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0xdfd8f77a, new int[] { 2, 8, 131, 11, 131, 143, 0 } },
            { 0xdfde21e4, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0xe075d96b, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xe0df7c0d, new int[] { 16, 2, 4, 12, 4, 4, 8 } },
            { 0xe0e2da61, new int[] { 1, 1, 1, 3, 1, 1, 1 } },
            { 0xe0f3926b, new int[] { 1, 1, 3, 3, 3, 3, 1 } },
            { 0xe0f69b8f, new int[] { 1, 2, 1, 0, 0, 0, 0 } },
            { 0xe112d26c, new int[] { 8, 32, 0, 0, 0, 0, 0 } },
            { 0xe14bfa66, new int[] { 8, 4, 130, 3, 130, 130, 130 } },
            { 0xe1577be5, new int[] { 64, 4, 131, 131, 131, 135, 131 } },
            { 0xe163b26c, new int[] { 8, 4, 130, 3, 130, 134, 134 } },
            { 0xe19c2cff, new int[] { 4, 2, 0, 1, 0, 8, 0 } },
            { 0xe1b42acd, new int[] { 8, 16, 0, 0, 0, 0, 0 } },
            { 0xe209959f, new int[] { 16, 128, 0, 0, 0, 128, 0 } },
            { 0xe23f9020, new int[] { 2, 32, 0, 1, 2, 0, 0 } },
            { 0xe4440b9b, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xe445b4a6, new int[] { 4, 2, 0, 1, 0, 12, 0 } },
            { 0xe482787c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe48328e1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe48c2a23, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe48d0746, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe4a3dca2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe4fb60eb, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe5293e72, new int[] { 64, 8, 131, 3, 131, 143, 143 } },
            { 0xe53b581a, new int[] { 0, 128, 131, 130, 130, 130, 143 } },
            { 0xe53fa2df, new int[] { 64, 1, 131, 131, 128, 135, 131 } },
            { 0xe542c148, new int[] { 1, 1, 1, 0, 0, 0, 0 } },
            { 0xe56eb692, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe5c378c5, new int[] { 1, 1, 3, 3, 3, 3, 3 } },
            { 0xe5d2ff1f, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0xe60e754c, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xe636aa91, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe6888167, new int[] { 16, 128, 4, 1, 4, 128, 8 } },
            { 0xe69e35c0, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0xe6e7f18b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe6fb10f3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe735b451, new int[] { 64, 8, 131, 131, 131, 135, 135 } },
            { 0xe7938987, new int[] { 32, 1, 3, 3, 3, 7, 135 } },
            { 0xe7d374d8, new int[] { 64, 2, 143, 131, 143, 131, 143 } },
            { 0xe7d90c21, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe82917b6, new int[] { 32, 1, 3, 3, 3, 3, 135 } },
            { 0xe89c0e48, new int[] { 16, 1, 132, 132, 132, 128, 128 } },
            { 0xe89ff41c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe8db9140, new int[] { 16, 128, 136, 136, 136, 128, 136 } },
            { 0xe8e88d38, new int[] { 8, 4, 131, 133, 131, 131, 131 } },
            { 0xe93c8663, new int[] { 128, 8, 2, 3, 2, 4, 2 } },
            { 0xe94250e9, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0xe9693f98, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe999932c, new int[] { 8, 4, 130, 3, 130, 6, 0 } },
            { 0xe9a90c22, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0xe9f11574, new int[] { 32, 1, 3, 3, 3, 3, 135 } },
            { 0xe9fc1f39, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xea2f9b26, new int[] { 64, 8, 0, 0, 0, 0, 0 } },
            { 0xea50c82f, new int[] { 0, 128, 131, 130, 130, 130, 143 } },
            { 0xea71c9dc, new int[] { 4, 2, 0, 1, 0, 8, 0 } },
            { 0xeac3d84f, new int[] { 16, 128, 140, 139, 140, 143, 143 } },
            { 0xeb686709, new int[] { 1, 1, 1, 0, 0, 0, 0 } },
            { 0xeb6c0a92, new int[] { 128, 8, 2, 3, 2, 135, 0 } },
            { 0xeb945bc1, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xebbd5efc, new int[] { 32, 128, 0, 1, 0, 0, 0 } },
            { 0xebdc8225, new int[] { 16, 128, 4, 136, 4, 12, 136 } },
            { 0xebe254b3, new int[] { 4, 1, 0, 9, 0, 128, 135 } },
            { 0xec0f97f3, new int[] { 0, 128, 2, 2, 2, 0, 2 } },
            { 0xec3b09c0, new int[] { 64, 2, 143, 131, 143, 131, 131 } },
            { 0xec86699b, new int[] { 64, 128, 131, 131, 131, 134, 131 } },
            { 0xeca8d234, new int[] { 0, 1, 132, 132, 132, 0, 128 } },
            { 0xecadc9fd, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0xecf7dde6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xed8bea88, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0xede77ffb, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0xee3f527d, new int[] { 64, 1, 131, 131, 131, 131, 135 } },
            { 0xee72ed45, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xee8dc760, new int[] { 0, 128, 4, 140, 4, 0, 0 } },
            { 0xeea7d425, new int[] { 64, 16, 0, 0, 0, 0, 0 } },
            { 0xeea95801, new int[] { 64, 128, 128, 136, 128, 8, 0 } },
            { 0xeebceb18, new int[] { 1, 8, 1, 9, 0, 0, 0 } },
            { 0xeec5748a, new int[] { 8, 2, 3, 3, 3, 3, 130 } },
            { 0xeeeb7597, new int[] { 8, 4, 128, 1, 128, 132, 128 } },
            { 0xefb6a70c, new int[] { 64, 2, 131, 131, 131, 131, 139 } },
            { 0xefba77ca, new int[] { 16, 128, 11, 139, 11, 131, 139 } },
            { 0xefbdcfbf, new int[] { 32, 2, 1, 1, 0, 129, 129 } },
            { 0xefc4c201, new int[] { 0, 128, 0, 0, 0, 0, 143 } },
            { 0xeff1fdb0, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0xf0ce033e, new int[] { 0, 8, 0, 0, 0, 0, 0 } },
            { 0xf0d9ae7f, new int[] { 16, 1, 4, 4, 4, 0, 0 } },
            { 0xf126ef7c, new int[] { 0, 0, 0, 132, 132, 0, 136 } },
            { 0xf14f637a, new int[] { 0, 2, 2, 2, 2, 2, 2 } },
            { 0xf157fd2a, new int[] { 0, 1, 0, 4, 0, 0, 4 } },
            { 0xf15b8010, new int[] { 8, 4, 128, 1, 128, 132, 130 } },
            { 0xf191c159, new int[] { 0, 1, 140, 136, 140, 128, 136 } },
            { 0xf1f37c98, new int[] { 2, 4, 130, 3, 130, 130, 128 } },
            { 0xf2010049, new int[] { 1, 1, 0, 0, 0, 0, 0 } },
            { 0xf219d380, new int[] { 0, 128, 4, 12, 4, 1, 1 } },
            { 0xf2999f51, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf2e97a59, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf3b82ddf, new int[] { 32, 2, 0, 1, 0, 1, 129 } },
            { 0xf3ca84b4, new int[] { 64, 8, 131, 11, 131, 11, 143 } },
            { 0xf3ccccbe, new int[] { 64, 8, 131, 11, 131, 11, 143 } },
            { 0xf3f43ce9, new int[] { 1, 1, 131, 131, 131, 131, 131 } },
            { 0xf3fab854, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xf4126cc4, new int[] { 0, 128, 0, 0, 0, 4, 0 } },
            { 0xf416b3ec, new int[] { 4, 2, 0, 1, 0, 12, 0 } },
            { 0xf4e5edc9, new int[] { 8, 4, 128, 129, 128, 132, 128 } },
            { 0xf538d3c7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf53eaf26, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf56ce72c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf5b89805, new int[] { 16, 128, 4, 140, 4, 8, 136 } },
            { 0xf60db645, new int[] { 0, 128, 2, 2, 2, 2, 2 } },
            { 0xf6159793, new int[] { 16, 128, 131, 131, 131, 139, 143 } },
            { 0xf61d92e8, new int[] { 8, 128, 0, 1, 0, 0, 0 } },
            { 0xf662fbb4, new int[] { 128, 2, 0, 0, 0, 0, 0 } },
            { 0xf718d902, new int[] { 64, 1, 128, 128, 128, 0, 128 } },
            { 0xf73f0765, new int[] { 64, 128, 0, 0, 0, 0, 0 } },
            { 0xf784f104, new int[] { 2, 8, 0, 1, 0, 0, 0 } },
            { 0xf78e916d, new int[] { 0, 2, 4, 12, 4, 2, 2 } },
            { 0xf78ed967, new int[] { 0, 2, 2, 2, 2, 2, 2 } },
            { 0xf78f37e6, new int[] { 64, 4, 135, 131, 135, 135, 139 } },
            { 0xf79a89c2, new int[] { 8, 2, 2, 3, 2, 135, 135 } },
            { 0xf7d7940c, new int[] { 64, 1, 131, 131, 131, 131, 131 } },
            { 0xf7f1f13c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf86f37e4, new int[] { 8, 128, 132, 140, 132, 136, 136 } },
            { 0xf88e6aac, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf8c07e5d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf8e3fc32, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0xf8f818e1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf9ca6b20, new int[] { 16, 128, 143, 3, 128, 0, 140 } },
            { 0xf9e11d1c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xfa733902, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xfac41fac, new int[] { 0, 128, 0, 128, 0, 132, 128 } },
            { 0xfaf6c024, new int[] { 8, 128, 2, 131, 2, 143, 0 } },
            { 0xfbc9619c, new int[] { 64, 1, 143, 131, 143, 135, 143 } },
            { 0xfc55fe0a, new int[] { 16, 128, 4, 12, 4, 4, 0 } },
            { 0xfc650ed2, new int[] { 32, 128, 1, 1, 1, 1, 1 } },
            { 0xfc697613, new int[] { 16, 0, 0, 0, 0, 0, 0 } },
            { 0xfc8d548b, new int[] { 8, 2, 131, 5, 131, 131, 131 } },
            { 0xfca517aa, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xfd03db16, new int[] { 16, 128, 131, 131, 131, 139, 143 } },
            { 0xfd746ad2, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0xfd938797, new int[] { 64, 2, 131, 131, 131, 131, 131 } },
            { 0xfddca51c, new int[] { 128, 2, 128, 1, 128, 0, 128 } },
            { 0xfdef7fc2, new int[] { 32, 1, 3, 3, 3, 3, 131 } },
            { 0xfe1a5de6, new int[] { 64, 1, 131, 131, 131, 135, 131 } },
            { 0xfe7b8edb, new int[] { 32, 128, 1, 3, 1, 1, 1 } },
            { 0xfe7c3fec, new int[] { 64, 2, 131, 131, 131, 135, 135 } },
            { 0xfea08f34, new int[] { 0, 128, 4, 12, 5, 1, 1 } },
            { 0xfeac70dc, new int[] { 8, 4, 128, 1, 128, 128, 128 } },
            { 0xfee30120, new int[] { 64, 8, 135, 3, 135, 143, 143 } },
            { 0xfefda8d6, new int[] { 0, 128, 1, 8, 1, 1, 1 } },
            { 0xff7d2ca4, new int[] { 64, 4, 131, 131, 143, 135, 143 } },
            { 0xff97ba8f, new int[] { 64, 8, 131, 3, 131, 143, 131 } },
            { 0xff9f18e0, new int[] { 1, 1, 1, 0, 0, 0, 0 } },
            { 0xffbb3b82, new int[] { 32, 2, 1, 1, 0, 1, 129 } },
            { 0xffbe1d84, new int[] { 64, 64, 0, 0, 0, 0, 0 } },
            { 0xffc0ea58, new int[] { 0, 128, 2, 2, 2, 0, 2 } },
        };



        // Per-fixture IFF-literal constants (Objects.far members, raw OBJD decode): eligible count,
        // sorted GUID list, sum(guid*price). Mounted recompute must equal these (any OBJD edit FAILS).
        // Round 48 catalog NAME IFF-LITERAL canon: generated by tools/iff_objd_names.py over
        // game-data/The Sims (1114 eligible GUIDs) - every served buy-catalog item Name must equal
        // the raw OBJD chunk LABEL exactly as the engine serves it (IObjectCatalogItem.Name =
        // obj.ChunkLabel; chunkLabel = ASCII.GetString(64 label bytes).TrimEnd(0), high bytes -> ?, tso.files IffFile.cs).
        public static readonly Dictionary<uint, string> CatalogNames = new Dictionary<uint, string>{
            { 0x000001ca, "Door - Window ( Federal )" },
            { 0x000c147f, "Magic - Bee Hive" },
            { 0x00230edc, "DollHouse" },
            { 0x00c2b68f, "Chairs - Living Room - Booth Tweener - Expensive" },
            { 0x00c8a5f1, "Stair - Beach" },
            { 0x00dd7908, "Tree - Curb - Cherry" },
            { 0x00ddfef9, "Toilet - Country" },
            { 0x00e22e1e, "Lamp - Sconce - Torch (Castle 1)" },
            { 0x00e30dc2, "Window - Tall French" },
            { 0x00fe9850, "Display Case - Masks" },
            { 0x01259297, "Table - End - Country 1" },
            { 0x0125da9d, "Table - End - Beach 1" },
            { 0x0133d442, "Dining Table - Very Expensive" },
            { 0x01f02564, "Sculptures - Animating Neon" },
            { 0x029c9043, "Sculptures - Retro 2" },
            { 0x02f04067, "Table - End - Expensive 2" },
            { 0x031db391, "Stair - Knotty Lodge" },
            { 0x048567b7, "Table - End - Buffet" },
            { 0x04d86d1f, "NPC - Unleashed - Pen - Tabby" },
            { 0x05777d82, "Pet - Cockatoo" },
            { 0x05d17306, "Column Arch - Deco" },
            { 0x05dac986, "NPC - Unleashed - Dog Two" },
            { 0x068ead71, "Punch Bowl" },
            { 0x0768a9cb, "Sofa - Loveseat - Cuddle" },
            { 0x09196e73, "Window - Carnival" },
            { 0x0924bc1f, "Chair - Living Room - Inflatable 3" },
            { 0x099a621e, "Table - Picnic" },
            { 0x09cc9d1c, "Columns - Inlaid" },
            { 0x09d8dcec, "Trees - Mulberry" },
            { 0x0a02f059, "Table - End - Barrel" },
            { 0x0a717442, "Column - Log" },
            { 0x0a8178e9, "Sink - Country" },
            { 0x0a87e8cf, "Table - End - Charm" },
            { 0x0ae5df54, "Koi Pond" },
            { 0x0aec8c97, "Table - Dining - Restaurant Cheap" },
            { 0x0afae8b0, "Stereo - Store Wall Cheap" },
            { 0x0b1ad6cc, "Trash - Downtown - Fancy" },
            { 0x0b36e878, "Painting - Magic - Spooky Painting" },
            { 0x0bfdfb2a, "Bar - Restaurant - Downtown" },
            { 0x0c04ca02, "Window - Castle 2" },
            { 0x0c0d4574, "Lamp - Unleashed - Candle - Black" },
            { 0x0c189d36, "Painting - Magic - Carnival Painting" },
            { 0x0ce4a17c, "Sculptures - Globe" },
            { 0x0d14b885, "Window - Vegas 1" },
            { 0x0d192399, "Coffee Table - Mission" },
            { 0x0d2dde73, "Door - Bamboo" },
            { 0x0d4ce6fd, "Window - Castle 1" },
            { 0x0d94642f, "Door - Roman Arch" },
            { 0x0dfd8714, "Lamp - Table - Vegas 1" },
            { 0x0e4f2e14, "Stereo - Unleashed - Gothic Wall" },
            { 0x0e82c943, "Bed - Single - Cheap" },
            { 0x0eb692fb, "Shrub - Bamboo" },
            { 0x0ecbec1c, "Trash - Vacation - Beach - Inside" },
            { 0x0ede95bb, "Window - Vegas 2" },
            { 0x0f1aa416, "Trash - Vacation - Outside" },
            { 0x0f77759b, "Sign - Vacation Island Logo" },
            { 0x0f8beda7, "Chair - Living Room - Inflatable" },
            { 0x0fbb8bf8, "Sofa - Expensive 1" },
            { 0x0fd4ccef, "Dresser - Cheap" },
            { 0x0fe5fa64, "Column - SciFi" },
            { 0x0ffc192a, "Fountain - Iron" },
            { 0x10444309, "Flowers - Outdoor - Gypsy" },
            { 0x108262be, "Lamp - Wall - Bar" },
            { 0x110cf4b1, "Trees - Spellbound - Autumn1" },
            { 0x112039a0, "Piano - Standing" },
            { 0x1181ccb1, "Fence - Plant Flora CounterBox French" },
            { 0x11a92d74, "Toy Box" },
            { 0x11ec66d9, "Shrub - Beach - Hau" },
            { 0x12a45904, "Awning - Beach" },
            { 0x12d9e6a3, "Chair - Restaurant - Expensive" },
            { 0x135e770f, "Clock - Cuckoo" },
            { 0x14284046, "Poster - Painting4" },
            { 0x144a7815, "Counter - Kitchen - Country" },
            { 0x144c3f7c, "Poster - Painting3" },
            { 0x1468c3a7, "Trees - Birch" },
            { 0x14ae1e58, "Window - Lighted - Expensive" },
            { 0x14ef6729, "Game - Strength-O-Meter" },
            { 0x151d17ed, "Table - Dining - Tuscan" },
            { 0x15316867, "Plant - Floor - Palm Tree" },
            { 0x15445522, "Plant - Floor - Bird of Paradise" },
            { 0x154522af, "Shrub - Juniper Bush" },
            { 0x15460fca, "Shrub - Sword Fern" },
            { 0x15597b59, "Sculptures - Magic - Balloons" },
            { 0x157021de, "Sculptures - Magic - Sign Ground Shopping" },
            { 0x15717e80, "Window - Loft" },
            { 0x1584c383, "Chair - Dining - Castle" },
            { 0x160bb3d1, "Dresser - Deco" },
            { 0x165cd872, "Pet - Food Bowl - Expensive" },
            { 0x1671a218, "Tree - Curb" },
            { 0x1694348f, "Display Case - Birds" },
            { 0x16aed8d8, "Painting - World Map" },
            { 0x16d3a20d, "Door - Retro 2" },
            { 0x16f14e74, "Door - Spooky" },
            { 0x171527eb, "Poster - Painting1" },
            { 0x17332210, "Flowers - Outdoor - Jasmine" },
            { 0x17579980, "Bed - Double - Castle" },
            { 0x1772dd4f, "Hot Tub Fame" },
            { 0x17f06fe1, "Poster - Painting2" },
            { 0x17fc774d, "Door - Bamboo Beaded" },
            { 0x17fd98f7, "Dresser - Modern" },
            { 0x182e7c24, "Pet Award Cabinet" },
            { 0x19057c09, "Lamp - Floor - Candle" },
            { 0x191b9479, "Painting - Admiral" },
            { 0x1ab85e45, "Archery" },
            { 0x1ad5b840, "Dining Table - Rave" },
            { 0x1b490662, "Painting - Abstract" },
            { 0x1b833d24, "Television - Wall Mount - Superstar" },
            { 0x1bb40a4e, "Fort - Water Balloon" },
            { 0x1bbdb732, "Counter - Kitchen - Rave" },
            { 0x1c6b6d6a, "Painting - Superstar - Music1" },
            { 0x1cc44f98, "Table - Dining - French" },
            { 0x1d772052, "Music Recording Studio" },
            { 0x1d8e6e89, "Table - End - Basket" },
            { 0x1de172c2, "Rug - Castle 2" },
            { 0x1dff3cab, "Counter - Bathroom - Moderate 1" },
            { 0x1e6d621b, "Sculptures - Chainsaw" },
            { 0x1e743c39, "Pet - Pedestal - Master" },
            { 0x1e8b9ea7, "Dance Floor" },
            { 0x1e8dd05c, "Sculptures - Venus De Milo" },
            { 0x1f15af7e, "Painting - Retro 2" },
            { 0x1f287b65, "Display Counter Top - Collars" },
            { 0x1fac199d, "Trash Compactor" },
            { 0x2077e7de, "Cart - Spellbound - Medicine Wagon" },
            { 0x2130c4e5, "Magic - Quest - Puzzle Box" },
            { 0x21332b58, "Door - Beaded" },
            { 0x21e65bbf, "Trash - Castle Barrel" },
            { 0x21f954a6, "Guitar - Electric" },
            { 0x2256ba70, "Window - Storm" },
            { 0x2263dceb, "Clock Post - Downtown" },
            { 0x228e96d2, "Chair - Dining - Beach - Outside" },
            { 0x22920f55, "Chair - Living Room - Expensive 1" },
            { 0x22dd513c, "Awning - Vacation" },
            { 0x23941850, "Door - Front" },
            { 0x243f0fa0, "Tent" },
            { 0x246f6a23, "DeadMouse" },
            { 0x24dad5c4, "Chair - Living Room - Inflatable 2" },
            { 0x2521000e, "Display Floor - Grocery" },
            { 0x253c1a5e, "Barbecue" },
            { 0x254233ad, "Sofa - Bamboo" },
            { 0x25b82d98, "Chair - Dining - Outdoor - Moderate" },
            { 0x25ba0090, "Table Gift Shop" },
            { 0x2644d3c7, "Food Counter - Pastry" },
            { 0x26ad0662, "Display Case - Toys" },
            { 0x26ad56ff, "Display Case - Jewelry" },
            { 0x26bfbb29, "Chair - Dining - Cheap" },
            { 0x26d24804, "Display Floor - Berries" },
            { 0x26e91ef5, "Display Case - Candy" },
            { 0x2768de7d, "Bed - Single - Trendy" },
            { 0x2796b726, "Chair - Restaurant - Cafe" },
            { 0x27f8f51f, "Column Arch - Goth" },
            { 0x28975923, "Lamp - Table - Very Expensive" },
            { 0x28a0403e, "Sculptures - Magic - Vase of Swords" },
            { 0x2935d383, "Door - Vegas 2" },
            { 0x294439e2, "Dining Table - Castle" },
            { 0x294a48ee, "Dresser - Child's" },
            { 0x29cb5a51, "Stereo - Jukebox" },
            { 0x29eaef1f, "Lamp - Wall - LED Pods" },
            { 0x2a262d32, "Window - Bamboo" },
            { 0x2a3d8714, "Magic - Cookbook" },
            { 0x2a7994bf, "Sculptures - Vase" },
            { 0x2a7d9829, "Wishing Well " },
            { 0x2ab64a1d, "Counter - Kitchen - BlueWhite" },
            { 0x2abc6e73, "Flowers - Outdoor - Gypsy2" },
            { 0x2aca7211, "Door - Wood" },
            { 0x2b4502a2, "Pet - Bed - Expensive" },
            { 0x2b49a3f4, "Column - Roman - Aqueduct" },
            { 0x2b83b971, "Trees - Apple" },
            { 0x2b89fd8a, "Fence - Brick" },
            { 0x2b956ab9, "Fun House 3" },
            { 0x2ba019c7, "Lamp - Table - French" },
            { 0x2baa0029, "Magic - Clown Portal" },
            { 0x2bb9b580, "Fence - Gothic" },
            { 0x2bb9e51d, "Fence - French" },
            { 0x2bd867fe, "Table - End - Moderate 2" },
            { 0x2be7cbba, "Recliner - Wicker" },
            { 0x2c501364, "Lamp - Floor - Expensive" },
            { 0x2c555ea3, "Shrub - Low Round" },
            { 0x2c57d9f1, "Toilet - Stall - Euro" },
            { 0x2c5ac343, "Lamp - Table - Moderate" },
            { 0x2c7dff0e, "Tub - Shower - Vacation" },
            { 0x2cfb155a, "NPC - Dragon - Good/Gold" },
            { 0x2d0a3de1, "Bar - Restaurant - Rave" },
            { 0x2d5f0d6c, "Podium - Dining - Expensive" },
            { 0x2d773bf9, "Lamp - Wall - Lodge" },
            { 0x2e10faec, "Mirror - Wall - Vacation" },
            { 0x2e453ea5, "Column - Round White" },
            { 0x2e7f37ea, "Painting - Bamboo Wall Unit" },
            { 0x2e9fdca5, "Sofa - SciFi" },
            { 0x2ecdb068, "Pet Bath" },
            { 0x2f014f68, "Fence - White Lattice" },
            { 0x2f42fc26, "Lamp - Floor - Vegas 1" },
            { 0x2f86506b, "Clothing Rack - Formal" },
            { 0x2f8715d1, "Window - Bay - Expensive" },
            { 0x2f92cd8f, "Painting - Seascape Shadowbox" },
            { 0x2fc581ad, "Painting - Pastel Beach" },
            { 0x2fcd8a30, "Chair - Dining - Gypsy" },
            { 0x2fdf80dd, "Cash Register - Vacation" },
            { 0x2ffaa585, "Painting - Mondrian" },
            { 0x3007996e, "Trees - Pine - Large" },
            { 0x3026a0f4, "Trees - Fir - Large" },
            { 0x307ddfa6, "Stair straight shot Medium" },
            { 0x3089d8d2, "Bed - Double - Cheap" },
            { 0x30bcda26, "Magic - Tea Set" },
            { 0x30f4b933, "Snake Charmer" },
            { 0x31a0e42c, "Pool - Diving Board" },
            { 0x31edeb80, "Haunted House 1" },
            { 0x3288ed87, "Stair - Spiral - Modern" },
            { 0x328ed9e1, "Sofa - Loveseat - Vegas" },
            { 0x32f5a529, "Recliner - Massage" },
            { 0x3313d86b, "Column Arch" },
            { 0x334f732f, "Sofa - Loveseat - Moderate 1" },
            { 0x342a5e00, "Sculptures - Vegas 1" },
            { 0x34431396, "Shrub - Lily Pad" },
            { 0x3480731c, "Counter - Kitchen - Castle Stone" },
            { 0x3484c1f7, "Trees - Crape Myrtle" },
            { 0x34acf253, "Painting - Black Light" },
            { 0x34f7be98, "Chair - Living Room - Modern" },
            { 0x34fe4e17, "Sink - Bathroom - Expensive" },
            { 0x354a1785, "Fence - Chain Link" },
            { 0x359e63bd, "Fence - Vacation" },
            { 0x35b26d7f, "Gargoyle Workbench" },
            { 0x35eaebdc, "Costume Trunk" },
            { 0x36113a6d, "Coffee Table - Cafe" },
            { 0x3658d1aa, "Counter - Kitchen - Beach" },
            { 0x3659dca2, "Flamingo" },
            { 0x3672a8b2, "Buffet Table - Studio" },
            { 0x36f33011, "Painting - Sled" },
            { 0x36fe2161, "Fence - Bamboo" },
            { 0x37186734, "Stair - Sweeping" },
            { 0x371e3b21, "Chair - Dining - French" },
            { 0x37326795, "Sculptures - Magic - Ride Sign" },
            { 0x3742732c, "Souvenir Cabinet - Floor Peacock" },
            { 0x375dd48b, "Sculptures - Castle 6" },
            { 0x3794bcc5, "Sink - Roman" },
            { 0x37bfa925, "Sculptures - Tiki Totem" },
            { 0x38232e50, "Sofa - Loveseat - Cafe" },
            { 0x384c487b, "Sculptures - Bubble Wall" },
            { 0x391e258e, "Dining Table - Vegas" },
            { 0x39262c4c, "Lamp - Wall - AOL" },
            { 0x39319021, "Trees - Pine" },
            { 0x398cc7a0, "Rug - Zebra" },
            { 0x39ce4936, "Awning - French Fabric" },
            { 0x39dd7192, "Sculptures - Fashion1" },
            { 0x39ed3b5a, "Sculptures - Mannequin" },
            { 0x3aa14a49, "SkeletonCloset" },
            { 0x3ac7f3be, "Pet - Parrot" },
            { 0x3b0971f3, "Stereo Speakers - Superstar" },
            { 0x3bc18581, "Sculptures - Horse Dock" },
            { 0x3c30cb5d, "Swing - Porch" },
            { 0x3c52b66d, "Fence - Plant Flora CounterBox" },
            { 0x3c566968, "Toilet - Expensive" },
            { 0x3c71d2e9, "Sofa - Wicker AOL" },
            { 0x3c9ea694, "Stair straight shot Dark" },
            { 0x3d1f6466, "Lamp - Floor - Studio" },
            { 0x3d3e7889, "Display - Floor - PetPenDog" },
            { 0x3dbfc043, "Menorah" },
            { 0x3dcc1abe, "Piano - Vegas Grand" },
            { 0x3dea8b15, "Fireplace - Lodge" },
            { 0x3def46a0, "Fireplace - Souvenir" },
            { 0x3e69a01d, "Chair - Living Room - Lodge" },
            { 0x3e6fda2f, "Table - End - Expensive 1" },
            { 0x4008dbb7, "Mirror - Store Wall" },
            { 0x404ee381, "Stove - Hooded - Expensive" },
            { 0x405046ff, "Toy Rocket" },
            { 0x40c778fd, "Magic - Minigolf - Dragon" },
            { 0x4126d70f, "Lamp - Hula" },
            { 0x417e691c, "Painting - Quilt - Wall" },
            { 0x4188ca2b, "NPC - Unleashed - Cat Three" },
            { 0x41a50757, "Window - Plate Glass - Big" },
            { 0x41c753c1, "Lamp - Post - Connecting Carnival" },
            { 0x41e1f6f3, "Fun House 1" },
            { 0x41fdd8d8, "Chair - Living Room - Castle 2" },
            { 0x4208b8e3, "Pool - Ladder" },
            { 0x42197abc, "Bookshelf - Retro" },
            { 0x42316f67, "Chair - Dining - Lodge" },
            { 0x42bcc380, "Column Arch - French" },
            { 0x42c078b3, "Fridge - Moderate" },
            { 0x431d46c8, "Chair - Dining - Wicker" },
            { 0x4371fde9, "Curtains - Velvet" },
            { 0x43a116b4, "Painting - Cafe - Art" },
            { 0x43a15ebe, "Painting - Cafe - Poster" },
            { 0x43b62717, "Sculptures - Windmill" },
            { 0x440087f3, "Trees - Prune" },
            { 0x4450e4e0, "Set - Photo Shoot" },
            { 0x44e8992a, "Window - Single Pane" },
            { 0x4596ee35, "Magic - Toy Box Faerie" },
            { 0x45caf478, "Food Counter - Vacation" },
            { 0x4699596d, "Food Counter - Ice Cream" },
            { 0x46ddd450, "Sink - Vacation" },
            { 0x46e807b4, "Lamp - Ceiling - Fan - French" },
            { 0x46ef5577, "Lamp - Ceiling - Expensive" },
            { 0x46f4e80b, "Stereo - Boombox" },
            { 0x471a0bf6, "Painting - Superstar - Fountain" },
            { 0x473532d6, "Fence - Stone" },
            { 0x47a99575, "Counter Top Display - Toys" },
            { 0x47e917c4, "Counter - Kitchen - Expensive #1" },
            { 0x47f39f51, "Pool Table" },
            { 0x481a74ec, "Pinball Machine" },
            { 0x48a153ad, "Rug - Fame" },
            { 0x49402e4f, "Awning - Carnival" },
            { 0x497a0c3e, "Fireplace - Cheap" },
            { 0x49f9c16f, "VooDoo Doll" },
            { 0x4a21d3a7, "Dining Table - Bamboo" },
            { 0x4a459a5d, "Lamp - Sconce - Castle 3" },
            { 0x4a4d1966, "Door - Warehouse" },
            { 0x4a70df92, "Pet - Dog - Template" },
            { 0x4a8ae8fb, "Table - Dining - Restaurant Expensive" },
            { 0x4ae4f77c, "Sculptures - Castle 2" },
            { 0x4af2067e, "Awning - Gypsy2" },
            { 0x4b4d30a9, "Shrub - Tall Round" },
            { 0x4b69ea5a, "Sculptures - Large Black Slab" },
            { 0x4b78bf0e, "Column - Vegas" },
            { 0x4bd0b785, "Column - Corinthian" },
            { 0x4be4809a, "Fun House 2" },
            { 0x4c103662, "Tub - Shower - Unleashed - Freestanding" },
            { 0x4c442733, "Counter - Unleashed - Pet Store" },
            { 0x4c443fa4, "Counter - Unleashed - Garden Store" },
            { 0x4c446f39, "Counter - Unleashed - Cafe" },
            { 0x4c659e7d, "Curtains - French Lace" },
            { 0x4c727c5d, "Sink - Kitchen - Cheap" },
            { 0x4cd0523d, "Mirror - Wall - Dressing Room" },
            { 0x4d6da494, "Set - Fashion Runway" },
            { 0x4ddf498c, "Garden Gnome - WB" },
            { 0x4deefc68, "Podium - Dining - Cheap" },
            { 0x4e1ea389, "Lamp - Floor - Blowfish" },
            { 0x4e665bf5, "Lamp - Unleashed - Candle - Skull" },
            { 0x4ee8b3cf, "Sink - Castle" },
            { 0x4ef4ea16, "Sofa - Loveseat - Cheap 1" },
            { 0x4ef80f89, "Fort - Snow" },
            { 0x4f51d969, "Column - Heating Vent" },
            { 0x4f8e1c8b, "Lamp - Wall - Sconce - AOL" },
            { 0x4fa133c9, "Window - Roman - Arched" },
            { 0x4fdec997, "NPC - Unleashed - Dog Three" },
            { 0x502367de, "Chair - Living Room - Retro 3" },
            { 0x50b9bc6d, "Sculptures - Movie2 - SEP6" },
            { 0x51474702, "Trees - Spruce" },
            { 0x5152a864, "Pet - Bed - Medium" },
            { 0x5156efbc, "Prize Booth - Vacation" },
            { 0x515e8635, "Food - Turkey" },
            { 0x5199e127, "Window - Federal" },
            { 0x51a767e2, "Window - Beach - Main" },
            { 0x51c23b85, "Fence - Iron" },
            { 0x52112235, "Sculptures - Kinetic" },
            { 0x5249b60e, "Mirror - Floor - Vacation" },
            { 0x5250b4c1, "Painting - M Abstract" },
            { 0x5295a7a8, "Stair - Retro" },
            { 0x52ae7a4e, "Spa - Tub - Single" },
            { 0x52c81f9b, "Pet - Play Fountain" },
            { 0x52ed4b7c, "Shrub - Beach - Ti" },
            { 0x52f6acd7, "Massage Table" },
            { 0x52f83bea, "Mailbox - Vacation" },
            { 0x5359bfc1, "Fence - Plant Box" },
            { 0x536a3e4d, "Cash Register - Downtown - Cheap" },
            { 0x547f4934, "Clothing Booth - Fame" },
            { 0x54b3b81a, "Clothing Rack - Generic" },
            { 0x54c3e0cf, "GardenPlot" },
            { 0x54c7f70d, "Arcade Game - Mars" },
            { 0x552a8297, "Magic - Spell Making - Wand Charger" },
            { 0x55acb9e0, "Door Double - Movie Grand" },
            { 0x55bcbc04, "Chair - Living Room - Cow" },
            { 0x56007c4b, "Door - Saloon" },
            { 0x56276590, "Bar - Beach" },
            { 0x578f5050, "Cart - Spellbound - Dragon" },
            { 0x57f68405, "Painting - Blinds - Vertical" },
            { 0x582f5936, "Toilet - Roman" },
            { 0x5880d445, "Painting - Castle 5" },
            { 0x58dde126, "Tub - Love - Hot Date" },
            { 0x59132604, "Sculptures - Roman - Augustus" },
            { 0x59133e93, "Sculptures - Roman - Sundial" },
            { 0x594a89f0, "Stereo - Unleashed - Pet Store Wall" },
            { 0x59b200b5, "Lamp - Wall - Warehouse" },
            { 0x59c389bc, "Chair - Office" },
            { 0x5a01eb5b, "Stand - Magazine" },
            { 0x5a12bc70, "Column - Bamboo" },
            { 0x5a1bc4bc, "Counter Top Display - Candy" },
            { 0x5a5cda09, "Dining Table - Cheap" },
            { 0x5a6feced, "Column - Rebar" },
            { 0x5a91cee0, "Open Mic" },
            { 0x5b048a52, "Lamp - Table - SuperStar" },
            { 0x5b088f5b, "Column - Fashion" },
            { 0x5b182c35, "Toilet - Gold" },
            { 0x5b561459, "Window - Gypsy With Candle" },
            { 0x5bfb6140, "Hot Tub - Vacation" },
            { 0x5c198cbe, "Trees - Willow" },
            { 0x5cb56b2d, "Fountain - Downtown - Large" },
            { 0x5cdc712f, "Bookshelf - Moderate" },
            { 0x5d071f26, "Lamp - Table - Lodge" },
            { 0x5d0bbdb6, "Painting - Castle 4" },
            { 0x5d39291c, "Window - Spooky With Candle" },
            { 0x5d6b538a, "Fireplace - Expensive" },
            { 0x5d723615, "Flowers - Outdoor - Coped - Orange" },
            { 0x5d767e1f, "Flowers - Outdoor - Coped - White" },
            { 0x5d7b6688, "Flowers - Outdoor - Coped - Violets" },
            { 0x5dac929f, "NPC - Unleashed - Raccoon" },
            { 0x5e8b157a, "Mechanical Bull" },
            { 0x5e9e42fc, "Piano - Black Grand" },
            { 0x5edb929d, "Painting - Tiki Mask" },
            { 0x5ede0bc4, "Sofa - Moderate 1" },
            { 0x5f421524, "Trees - Pine" },
            { 0x5fa381c1, "Television - Moderate" },
            { 0x5faa302a, "Bar - Wet - Saloon" },
            { 0x5fb93288, "Painting - Marlin Stuffed" },
            { 0x601b0c9a, "Rug - Retro" },
            { 0x6065ac2d, "Window - Lodge - Small" },
            { 0x60909779, "Door - Lodge - Main" },
            { 0x60d99178, "Igloo" },
            { 0x60e23da6, "Table - Picnic - Beach" },
            { 0x6106a1a7, "Counter - Vacation" },
            { 0x615c9169, "Window - Lodge - Large" },
            { 0x6188f5d7, "Magic - Minigolf - Windmill" },
            { 0x61b7982e, "Column - Roman - Tiled" },
            { 0x629be21f, "Trash Compactor - Tuscan" },
            { 0x62b6f3c1, "Moose Head" },
            { 0x62ccb570, "Cash Register - Spellbound" },
            { 0x62f8a7e4, "Door Double - Movie" },
            { 0x6350e96f, "Display - Floor - PetPenCat" },
            { 0x637692ea, "Display - Floor - Painting - Magic" },
            { 0x63a3d7d4, "Door - Star" },
            { 0x645365da, "Chair - Dining - Deco" },
            { 0x645542df, "Scuba Tank" },
            { 0x64983e4d, "Pet - Bird Cage Small" },
            { 0x64a10860, "Window - Spooky Shuttered" },
            { 0x64f0e2bc, "Sculptures - Magic - Sign Magic" },
            { 0x65274a4f, "Flowers - Outdoor - Tulips" },
            { 0x65396e67, "Sculptures - Bronze Dog" },
            { 0x6585d104, "Chair - Living Room - Furry" },
            { 0x6593d291, "Bed - Double - Expensive" },
            { 0x65cb152b, "Dressing Booth Beach" },
            { 0x66025e42, "Door - Portal" },
            { 0x6610c43c, "Lamp - Ceiling - Fan" },
            { 0x66131ba8, "Front Desk - Beach" },
            { 0x66237c5d, "Chair - Dining - Dive Bar" },
            { 0x663eaffc, "Bed - Double - Heart" },
            { 0x66548be0, "Sofa - Loveseat - SEP6 - DiveBar" },
            { 0x66877bef, "Clothing Rack - Fame" },
            { 0x6691c3d8, "Cart - Spellbound - Faerie" },
            { 0x67406e73, "Sculptures - Magic - Crypt" },
            { 0x675c18af, "Fridge - Cheap" },
            { 0x678f09ff, "Trees - Superstar - Orange" },
            { 0x6854da61, "Trees - Unleashed - Sycamore" },
            { 0x68897ff8, "Magic - Food Making - Baker's Oven" },
            { 0x68af92a3, "Park Rocker Horse" },
            { 0x68c11912, "Coffee Table Expensive" },
            { 0x6923633a, "Cash Register - Unleashed" },
            { 0x6928408e, "Mask - Unleashed - Zeus" },
            { 0x6954e030, "Phone - Pay Booth - Vacation" },
            { 0x69bd37af, "Teleporter - Single Player" },
            { 0x6a3889f8, "Chair - Dining - Expensive" },
            { 0x6a53de3c, "Display Case - Fish" },
            { 0x6a55e034, "Christmas Tree" },
            { 0x6a776e73, "Arcade Cabinet - FortuneTeller" },
            { 0x6a8594ad, "Karaoke Machine" },
            { 0x6ad59dd3, "Table - End - Modern" },
            { 0x6aeb3d99, "Magic - Minigolf" },
            { 0x6b264238, "Phone - Wall" },
            { 0x6bad868b, "Hydrant - Unleashed" },
            { 0x6bf6d402, "Chair - Dining - Pressed Metal" },
            { 0x6c1bb706, "Crystal Ball" },
            { 0x6c3bbc3e, "Lamp - Wall - Modern" },
            { 0x6c582f42, "Toilet - Stall - Studio" },
            { 0x6c7caec2, "Lamp - Table - Retro 1" },
            { 0x6c86123e, "Christmas 2001 - Mistletoe" },
            { 0x6c8bd4a3, "Door - Womens Room" },
            { 0x6c9f7100, "Sculptures - Music1" },
            { 0x6cb7f434, "Lamp - Wall - Deco" },
            { 0x6cc079c2, "Chair - Office Chair - Online" },
            { 0x6d96dcfa, "Stair straight shot Light" },
            { 0x6da8b998, "Chair - Dining - Folding" },
            { 0x6dd88e6d, "Phone - Wall - Payphone" },
            { 0x6e0ec37e, "Tub - Roman" },
            { 0x6e325f22, "Painting - Superstar - Fashion1" },
            { 0x6f2bb8ed, "Door - Bars" },
            { 0x6f35df40, "Door - Mens Room" },
            { 0x6f93a84a, "Column - Retro" },
            { 0x6fb00726, "Sink - Bathroom - Chrome" },
            { 0x702851b4, "Sculptures - Music2" },
            { 0x70467b85, "Studio MiniTrailer" },
            { 0x7051ee96, "Front Desk - Vacation" },
            { 0x70760da9, "Painting - Superstar - Fashion2" },
            { 0x70b0d5f2, "Display Case - Dog Collars" },
            { 0x70fc19c3, "Plant - Floor - Tuscan" },
            { 0x718ea9c7, "Painting - Blinds - Horizontal" },
            { 0x722a0fbf, "Halfpipe - Snowboard" },
            { 0x724ca405, "Painting - Pet Store - Small" },
            { 0x7280cbfc, "Chair - Dining - Bamboo" },
            { 0x72a5cbe3, "Dartboard" },
            { 0x72bdbb7d, "Shrub - Snowy - Tumbleweed" },
            { 0x72fa6739, "Magic - Spinning Wheel" },
            { 0x7318c9df, "NPC - Unleashed - Cat Two" },
            { 0x7387f3a2, "Popcorn Maker" },
            { 0x738a419a, "Bed - Single - Iron" },
            { 0x73d6180d, "Counter - Kitchen - Expensive #2" },
            { 0x73eef299, "Stair - Spellbound - Rickety" },
            { 0x74d93e95, "Table - Dining - Dive Bar" },
            { 0x74e7ffa9, "Phone - Pay Booth" },
            { 0x75ac188c, "Sink - Kitchen - Expensive" },
            { 0x75b0b2b7, "Fence - Split Rail" },
            { 0x765bcec0, "Magic - Butterchurn" },
            { 0x7726e3a6, "Dresser - Moderate" },
            { 0x77d26881, "Pool - Slide" },
            { 0x77d429bb, "Lamp - Wall - Kerosene" },
            { 0x77d9944d, "Table - Dining - Restaurant Cafe" },
            { 0x7839c094, "Painting - Roman - Frieze" },
            { 0x78d7ea4d, "Stand - Flower" },
            { 0x78edcd25, "Plant - Table - Violet" },
            { 0x7939d97d, "Table - End - Cheap 1" },
            { 0x795e70b6, "Chair - Living Room - Castle 3" },
            { 0x79e2bb28, "Window - Bars" },
            { 0x79fa7e6b, "Rug - Vegas" },
            { 0x7a56922f, "Pet - Bed - Cat house - Med" },
            { 0x7a8fdbf4, "Game - Chicken Toss" },
            { 0x7ab03de4, "Rug - Bear" },
            { 0x7b21053a, "NPC Robot - Dock" },
            { 0x7b6c8c63, "Counter - Vacation - Bathroom" },
            { 0x7bea0977, "Pet - Cat - Template" },
            { 0x7bfdcc88, "Shack - Rental - Vacation" },
            { 0x7c21272e, "Sofa - Loveseat - Roman" },
            { 0x7c24a376, "Shrub - Bush - Autumn" },
            { 0x7c5e5c6d, "Chair - Living Room - Cafe" },
            { 0x7cab54a2, "Door - Pirate" },
            { 0x7cb11019, "Bench - Garden 1" },
            { 0x7d2089b6, "Painting - C Velvet Clown" },
            { 0x7d2995e4, "Lamp - Ceiling - Cheap" },
            { 0x7d4a0f76, "Park Grill" },
            { 0x7d9b1a47, "Window - Pet" },
            { 0x7e09517e, "Lamp - Wall - Neon" },
            { 0x7e1f25b1, "Play Structure" },
            { 0x7e56b1be, "Lamp - Ceiling - Lodge" },
            { 0x7e853dea, "Stereo Speakers" },
            { 0x7e97444a, "Sofa - Cheap 2" },
            { 0x7ed5d1b6, "Toilet - Spellbound - Portable Gypsy" },
            { 0x7ef99b7e, "Magic - Minigolf - Clown" },
            { 0x7f0abd26, "Window - Retro 2" },
            { 0x7f1ae4cf, "Sign - Building - GiftShop" },
            { 0x7f283620, "Dining Table - Retro 1" },
            { 0x7f775eee, "Bookshelf - Bard Bust" },
            { 0x7fab4493, "Lamp - Wall - Blue Dish" },
            { 0x7fbe5529, "Lamp - Wall - White Globe" },
            { 0x7fd422a4, "Lamp - Wall - Oval Glass" },
            { 0x7fdf0fc1, "Lamp - Wall - Brass" },
            { 0x801f7235, "Counter - Shop - Medium" },
            { 0x80858fd1, "Barbeque - Vacation" },
            { 0x81179ab3, "Bed - Single - Child" },
            { 0x813a5a6b, "Birthday Cake - Cake" },
            { 0x81a87bab, "Photo - Camera" },
            { 0x81b3a551, "Pet - Bed - Cat house" },
            { 0x81c7a5e0, "Athletics Bench" },
            { 0x81d97262, "Stereo - Superstar" },
            { 0x823a9ee0, "Sofa - Loveseat - Rave2" },
            { 0x82e04c5b, "Bed - Double - Moderate" },
            { 0x82e0f146, "Chair - Living Room - Cheap 1" },
            { 0x83023da6, "Tub - Castle" },
            { 0x835ba084, "Computer - Moderate" },
            { 0x83e0af58, "Trash Can Inside" },
            { 0x83eb896c, "Fence - Snow Bank" },
            { 0x849207af, "Fridge - Tuscan" },
            { 0x84ceb10b, "NPC Entertainer - Cake" },
            { 0x84e0774c, "Toilet - Cheap" },
            { 0x852df95d, "Toilet - Aluminum" },
            { 0x855cde99, "Painting - Retro 3" },
            { 0x8592f25b, "Pet - Iguana Tank" },
            { 0x85b424a2, "Counter Top Display - Post Cards" },
            { 0x85b46ca8, "Counter Top Display - Kitsch" },
            { 0x85e00942, "Tub - Cheap" },
            { 0x85e02a40, "Shower" },
            { 0x85e4adbe, "Phone - Studio Town" },
            { 0x85f1a085, "Coffee - Regular" },
            { 0x8613a26a, "Phone - Pay Booth - Neighborhood" },
            { 0x862ccc31, "Window - Retro 3" },
            { 0x86e0bbb5, "Phone - Table" },
            { 0x877feca1, "Sculptures - Castle 4" },
            { 0x87c44d57, "Chair - Dining - Director Seat" },
            { 0x87c99127, "Pantry - Rustic" },
            { 0x87d00adc, "Sofa - Expensive 2" },
            { 0x880bb75e, "Magic - Food Making - Nectar Press" },
            { 0x88103f3e, "Sculptures - Female" },
            { 0x882480a6, "Stove - Retro" },
            { 0x88417fac, "Souvenir Cabinet - Wall Nice" },
            { 0x8841dbb9, "Game - Balloon Pop" },
            { 0x88734ce8, "Set - Movie" },
            { 0x88b6a098, "Stair - Spiral - Superstar" },
            { 0x88ede461, "Door - Castle 3" },
            { 0x89173b24, "Painting - Country - Long Horn Antlers" },
            { 0x8942bdc7, "Window - Victorian" },
            { 0x8a712a85, "Sculptures - Stuffed Animal" },
            { 0x8ad987b8, "Cash Register - Fame" },
            { 0x8b04d5e1, "Sculptures - Tragic Clown" },
            { 0x8ba56284, "Trees - Fir" },
            { 0x8bd70015, "Counter - Kitchen - Cheap 1" },
            { 0x8bf2e852, "Window - Music" },
            { 0x8c44ba58, "Window - Castle 3" },
            { 0x8c9350d2, "Recliner - Redwood" },
            { 0x8c9e33c9, "Dining Table - Outdoor Moderate" },
            { 0x8cb4d449, "Door - Lodge - Room" },
            { 0x8d1b6101, "Column Arch - Carnival 2" },
            { 0x8d4ee7df, "Sculptures - Movie1 - SEP6" },
            { 0x8d70f7b3, "Painting - Vegas 3" },
            { 0x8da9ffb4, "Stereo - Store Wall Expensive" },
            { 0x8dab571f, "Painting - Flowers" },
            { 0x8e3520bd, "Buffet Table" },
            { 0x8e880484, "Door - Gypsy1" },
            { 0x8e8d9ca9, "Arcade Cabinet - Cyclops" },
            { 0x8eaa68ad, "Fireplace - Moderate" },
            { 0x8eb8e1a6, "Plant - Flowers - Urn" },
            { 0x8ec5750d, "Magic - Spellbook" },
            { 0x8eec19f7, "Pet - Food Bowl - Cheap" },
            { 0x8f01bdcb, "Chair - Living Room - Deco" },
            { 0x8f033944, "Clock - Grandfather" },
            { 0x8f300ef1, "Telescope" },
            { 0x8f667b23, "Lamp - Floor - Animated - Rotating Tree" },
            { 0x8fb1bf00, "Awning - Gypsy1" },
            { 0x8fca5a7b, "Door - Cafe" },
            { 0x8fed54c2, "Hot Tub" },
            { 0x8ff0ca39, "NPC - Unleashed - Cat Four" },
            { 0x9082401f, "Sofa - Loveseat - Cheap 2" },
            { 0x90af09f4, "Curtains - Superstar - Red Velvet" },
            { 0x90c1c7cb, "Chess Table - Stone" },
            { 0x90c7c9e4, "NPC - Unleashed - Dog Four" },
            { 0x912cbed4, "Window - Castle 4" },
            { 0x91767a36, "Counter - Kitchen - Moderate" },
            { 0x91ed6984, "Slot Machine" },
            { 0x921fda07, "Dining Table - Moderate" },
            { 0x9293b59e, "Painting - Neon" },
            { 0x92c87dc0, "Lamp - Wall - Beach" },
            { 0x9367e97a, "Clock Post - Swiss" },
            { 0x93f98c8a, "Painting - Roman - Tapestry" },
            { 0x94082f6d, "Magic - Arena" },
            { 0x948aed3c, "Stair - Spiral - Indoor" },
            { 0x94aa32f4, "Chess Table" },
            { 0x94d2df9d, "Lamp - Floor - MovieSet" },
            { 0x94f5cda8, "Lamp - Floor - Vegas 2" },
            { 0x94fd43dc, "Painting - Navajo Rug - Wall" },
            { 0x952bcac7, "Plant - Floor - Floral Spray" },
            { 0x95624034, "Painting - Magic - Stage Tapestry1" },
            { 0x95a59fd2, "Window - Movie" },
            { 0x95b09639, "Display Counter Top - Grocery" },
            { 0x95d4de33, "Display Counter Top - Berries" },
            { 0x95efe015, "Sculptures - Castle 3" },
            { 0x9667a489, "Stair - Bamboo" },
            { 0x969bde47, "Rug - Daisy" },
            { 0x96a5928b, "Booth - Dining - Downtown - Cheap" },
            { 0x96c58db2, "Sink - Aluminum" },
            { 0x96cb67ae, "Pet - Dog House Expensive" },
            { 0x96e3398d, "Flowers - Outdoor - Wild" },
            { 0x97a845c3, "Volleyball Court" },
            { 0x984671a9, "Plant - Potted - Superstar" },
            { 0x98a0a6d6, "Lamp - Table - Candle Dive Bar" },
            { 0x98e09eb3, "Lamp - Floor - Cheap" },
            { 0x98e0f8bd, "Aquarium" },
            { 0x98e9cfe2, "Mirror - Wall - Castle" },
            { 0x99120547, "Chair - Living Room - Expensive 2" },
            { 0x99400b4a, "Trees - Cactus" },
            { 0x994fb8f8, "Chair - Living Room - Retro 2" },
            { 0x996de865, "Chair - Living Room - Retro 1" },
            { 0x997beef4, "Dresser - Retro 1" },
            { 0x9985a06f, "Chair - Living Room - Castle 1" },
            { 0x99e0a9b1, "Table - End - Moderate 1" },
            { 0x9a0d8249, "Door - Wood 2" },
            { 0x9a37fba7, "Fence - Carnival" },
            { 0x9aa2312f, "Display Case - Turtle" },
            { 0x9ae018bb, "Plant - Floor - Rubber" },
            { 0x9b3340b9, "Display Case - Seeds" },
            { 0x9b3a5cdf, "Bookshelf - Cheap" },
            { 0x9b3cdd30, "Sofa - Loveseat - SEP6 - Bench" },
            { 0x9be0e2bf, "Sofa - Cheap 1" },
            { 0x9c223167, "Lamp - Floor - Umbrella" },
            { 0x9c541464, "Fence - Rickety" },
            { 0x9c59140e, "Souvenir Cabinet - Floor Basic" },
            { 0x9cb1bb9b, "Dance Cage" },
            { 0x9d4b03a5, "Medicine Cabinet" },
            { 0x9da7b210, "Trees - Beverly Cypress" },
            { 0x9ede28ae, "Column - Square Wood" },
            { 0x9ee7eb60, "Door - Beach - Lobby" },
            { 0x9f12673e, "Sofa - Moderate 2" },
            { 0x9f3a38b9, "Counter - Wall" },
            { 0x9f7295a9, "Display Counter Top - Candles" },
            { 0x9f733d4f, "Cart - Unleashed - Vegetable" },
            { 0x9fa148dc, "Food Processor" },
            { 0x9fb223ce, "Easel" },
            { 0x9fd45e39, "Fountain Pool - Superstar" },
            { 0xa01e500d, "Dishwasher expensive" },
            { 0xa022c70c, "Fence - Party Lights" },
            { 0xa02c8857, "Trees - Palm" },
            { 0xa072951f, "Table - Dining - Unleashed  " },
            { 0xa08dd989, "Lamps - Street - Vacation" },
            { 0xa08e0c14, "Sofa - Lodge" },
            { 0xa0af4e74, "Sculptures - Magic - Clown" },
            { 0xa0ee756f, "Sculptures - Dress Torso" },
            { 0xa11d2875, "Painting - Castle 3 - Tapestry" },
            { 0xa13f39cf, "Painting - Castle 2" },
            { 0xa1594e42, "Painting - Retro 1" },
            { 0xa1755ff7, "Counter - Shop - Cheap" },
            { 0xa1bd4c80, "Painting - Vegas 2" },
            { 0xa1d5048a, "Painting - Castle 1" },
            { 0xa1d6fe56, "Chair - Theater seat deco" },
            { 0xa22c5865, "Lamp - Banner Lamp" },
            { 0xa2666952, "Painting - Vegas 1" },
            { 0xa2daa08c, "Flowers - Outdoor - Daffodils" },
            { 0xa2fe803a, "Stair - Sweeping - Tuscan" },
            { 0xa46aed33, "Counter - Shop - Lingerie" },
            { 0xa478d407, "Sculptures - Vegas 3" },
            { 0xa483d725, "Clock - Alarm" },
            { 0xa489640d, "NPC - Dragon - Slacker/Purple" },
            { 0xa4d8c299, "Sofa - Bench Stone" },
            { 0xa4e75cff, "Lamp - Floor - Animated - Torch" },
            { 0xa5004e8c, "Oxygen Bar" },
            { 0xa5b9515a, "Celeb Award Cabinet" },
            { 0xa5c0bb1e, "Chess Set Checkers" },
            { 0xa607cc98, "Dresser - Castle 1" },
            { 0xa67ac823, "Window - Rectangular High" },
            { 0xa6a4a1ac, "NPC - Unleashed - Pen - Labrador" },
            { 0xa6aebf5b, "Lamp - Floor - Castle 2" },
            { 0xa6caf1dd, "Dresser - Armoire Vegas 1" },
            { 0xa7340ec3, "Sink - Kitchen - Tuscan" },
            { 0xa77f36ae, "Pet - Maternity Box" },
            { 0xa7a70f83, "Snowslide" },
            { 0xa805e814, "Sculptures - Vegas 2" },
            { 0xa82ca08e, "Fountain" },
            { 0xa8ad8714, "Column - Castle 1 - Thick" },
            { 0xa8bdd25b, "Counter Top Display - Jewery" },
            { 0xa8c59f83, "Column - Castle 2 - Thin" },
            { 0xa90a7d01, "Desk - Expensive" },
            { 0xa91403ab, "Bookshelf - Western" },
            { 0xa91d4963, "Bookshelf - Rave" },
            { 0xa94c3259, "Chair - Dining - Spook Bones" },
            { 0xa9c05832, "Pool Table Deco - SuperStar" },
            { 0xa9ec8419, "Sofa - Cow" },
            { 0xaa2f9672, "Stereo - Expensive" },
            { 0xaa655e9f, "Column Arch - Spooky" },
            { 0xaa75a097, "Column - Square Brick" },
            { 0xaa86ad2a, "Trees - Spruce - Large" },
            { 0xaae98936, "Music Video Set" },
            { 0xaaf447d6, "Desk - Castle" },
            { 0xab101956, "Dining Table - Wicker" },
            { 0xab647747, "Alarm - Burglar" },
            { 0xabe586cd, "Painting - X Abstract" },
            { 0xabfc34d3, "Chair - Living Room - SciFi" },
            { 0xabfc4be9, "Chair - Living Room - Roman" },
            { 0xac262843, "Pet - Fish Bowl" },
            { 0xac66d6ca, "Lamps - Street - Tree" },
            { 0xacee4ba5, "Aquarium - Fame" },
            { 0xad005811, "DisplayFloorPoster" },
            { 0xad08fdc0, "Stove - Toaster Oven" },
            { 0xad0b9dd6, "Canning Station" },
            { 0xad12e0d0, "Plant - Table - Spider" },
            { 0xad2c5018, "Mask - Unleashed - Elephant" },
            { 0xad4cf878, "Stereo - Superstar - Wall" },
            { 0xad4d0d62, "Sofa - Loveseat - Moderate 2" },
            { 0xad591aac, "Flowers - Vase - Roses" },
            { 0xad67a694, "Sign - Building - Florist" },
            { 0xad67be03, "Sign - Building - Restaurant" },
            { 0xad67ee9e, "Sign - Building - Ice Cream" },
            { 0xae12c97a, "Counter - Superstar - Boutique" },
            { 0xae1daf72, "Recliner - Beach" },
            { 0xae5c99e7, "Counter - Superstar - Spa" },
            { 0xae6e43d6, "Painting - Magic - Stage Tapestry2" },
            { 0xaed36fcb, "Food Counter - Cotton Candy" },
            { 0xaed3c88a, "Shrub - Snowy - Cypress" },
            { 0xaf4a1300, "Plant - Floor - Cactus" },
            { 0xaf85a57c, "Dishwasher - Tuscan" },
            { 0xafabcc3d, "Computer - Very Expensive" },
            { 0xb01c2620, "Sign - Building - Market" },
            { 0xb0706e2a, "Sign - Building - Cafe" },
            { 0xb1153e7d, "Shrub - Cat Tails" },
            { 0xb1230866, "Food Counter - Goulash" },
            { 0xb159c37d, "Clock - Dive Bar" },
            { 0xb1b176bd, "Sign - Building - PetStore" },
            { 0xb21b9510, "Lamp - Unleashed - Wall Victorian" },
            { 0xb26ab256, "Pet Gym" },
            { 0xb31edff8, "Painting - Superstar - Platinum Record" },
            { 0xb346903c, "Sculptures - Bust" },
            { 0xb3760ba8, "Mask - Unleashed - Norleans" },
            { 0xb407a783, "Painting - PetStore - Large" },
            { 0xb46b0e55, "Painting - Superstar - Pilaster" },
            { 0xb48a1144, "Sculptures - Castle 1 - Head Jar" },
            { 0xb499ec29, "Train Set - Expensive" },
            { 0xb4b333e4, "Sculptures - Cornstalk" },
            { 0xb5215809, "Fireplace - Very Expensive" },
            { 0xb5250c97, "Painting - Superstar - Movie1" },
            { 0xb53903aa, "Window - Warehouse" },
            { 0xb56a0aff, "Pet - Bird Cage Large" },
            { 0xb6236b64, "Chair - Living Room - Vegas 1" },
            { 0xb62454df, "Bar - Wet - Vegas" },
            { 0xb668dd56, "Door - Vegas 1" },
            { 0xb78e7f8d, "Picnic Basket - Polar Bear" },
            { 0xb790a819, "Door - Castle 1" },
            { 0xb796426e, "Sculptures - Magic - Sign Ground Gypsy" },
            { 0xb797465f, "Painting - Superstar - Gold Record" },
            { 0xb79f3182, "Clock - Neon Wall" },
            { 0xb7cd406c, "Toilet - Urinal" },
            { 0xb7eaacba, "Door - Retro 1" },
            { 0xb80950cc, "Haunted House 3" },
            { 0xb867117e, "Train Set - Cheap" },
            { 0xb87a8d3f, "Lamp - Sconce - Castle 4" },
            { 0xb89b2451, "Sculptures - Perpetual Motion" },
            { 0xb9020120, "Trees - Spellbound - Autumn2" },
            { 0xb94755df, "Fridge - Expensive" },
            { 0xb99d5c6e, "Sofa - Retro 1" },
            { 0xb9b0c862, "Fence - Castle" },
            { 0xba67ead2, "Lamp - Garden" },
            { 0xba6e27bb, "Window - Deco" },
            { 0xba71a336, "Lamp - Table - Camping" },
            { 0xba7e8047, "Souvenir Cabinet - Wall Basic" },
            { 0xba9dc16a, "Basketball - Standard" },
            { 0xbb0846ea, "Fountain - Superstar" },
            { 0xbb1bcf68, "Table - End - Deco" },
            { 0xbb626ef3, "Stove - Microwave" },
            { 0xbb83a08a, "Shrub - Hedge Low" },
            { 0xbba46c20, "Download - No Pets Sign" },
            { 0xbc12cd61, "Awning - Expensive" },
            { 0xbc3ba088, "Painting - Still Life Fruit" },
            { 0xbc4b1c4f, "Bed - Double - Lodge" },
            { 0xbc4b7a23, "Bed - Double - Beach" },
            { 0xbc537c52, "Pet - Dog House Cheap" },
            { 0xbc661500, "Plant - Table - Carnivore" },
            { 0xbcab486a, "Chair - Dining - Mod" },
            { 0xbcc81aff, "Window - Mod" },
            { 0xbd78c75d, "Magic Growth - Root" },
            { 0xbdcf6469, "Stair straight shot" },
            { 0xbde052c5, "Door - Western" },
            { 0xbe1d7003, "Sculptures - Wind Chimes" },
            { 0xbe773f58, "Trash - Downtown - Cheap" },
            { 0xbeb0f165, "Window - Fashion" },
            { 0xbedd7b26, "Pet - Cat - Litter Box" },
            { 0xbf137195, "Coffee - Espresso" },
            { 0xbf1b918e, "Stair - Metal" },
            { 0xbf51b5f3, "Chair - Living Room - Toadstool" },
            { 0xbfc58dd0, "Game - Whack A Will" },
            { 0xc02b406a, "Flowers - Outdoor - Nasturtium" },
            { 0xc041ed5b, "Mirror - Wall - Moderate" },
            { 0xc0e20936, "Sculptures - TVCamera" },
            { 0xc0e64804, "Recliner - Double - Vacation" },
            { 0xc192dacc, "Television - Cheap" },
            { 0xc1b6da6a, "Dining Table - Expensive" },
            { 0xc25fcf86, "Lamp - Sconce - Castle 2" },
            { 0xc26265b3, "Rug - Castle 1" },
            { 0xc2a91fd0, "Bar - Wet" },
            { 0xc2f1a050, "Chair - Restaurant - Cheap" },
            { 0xc349c14c, "Front Desk - Lodge" },
            { 0xc3927fa3, "Door - French" },
            { 0xc3efd759, "Fence - Plant Flora CounterBox Vacation" },
            { 0xc4370e53, "Fishing Pier" },
            { 0xc4387388, "Door - Front - Deco" },
            { 0xc48f31a0, "Sofa - Castle 1" },
            { 0xc4c35536, "Rug - Braided Rag" },
            { 0xc523d73d, "Trash - Unleashed - Community Outside" },
            { 0xc56562c1, "Door - Glass Fashion" },
            { 0xc58b5c78, "Sofa - Loveseat - Castle 1" },
            { 0xc5d8a768, "Window - French" },
            { 0xc606154b, "Trash Can Inside clone" },
            { 0xc6448ae4, "Window - Cafe" },
            { 0xc69b6e73, "Door - Gypsy2" },
            { 0xc6b2bdd5, "Columns - Iron" },
            { 0xc71e43cd, "Mask - Unleashed - Red Beak" },
            { 0xc73c14a4, "Rug - Furry" },
            { 0xc74e7da7, "Clothing Rack - Swim" },
            { 0xc7a7215c, "Aquarium - Downtown - Big" },
            { 0xc7d177e1, "Painting - Soda" },
            { 0xc7d6cf27, "Lamp - Wall - Security" },
            { 0xc7efe216, "Fun House - Track - Left Turn" },
            { 0xc884fc9e, "Lamp - Lava" },
            { 0xc8ebdd42, "Coffee Table Moderate" },
            { 0xc96b93eb, "Guinea Pig - Cage" },
            { 0xc99ac74e, "Trees - Poplar" },
            { 0xca528b1d, "Sign - Building - Spooky" },
            { 0xca55ed92, "Rug - Spellbound" },
            { 0xcaf38382, "Chair - Dining - Rave" },
            { 0xcb386e73, "Lamp - Wall - Spooky" },
            { 0xcb6e77ab, "Table - End - Music" },
            { 0xcb790e58, "Window - Vent Fan" },
            { 0xcbc6c317, "Sign - Building - Carnival" },
            { 0xcbd2be4e, "Lamps - Street - Gas" },
            { 0xcbe53119, "Chair - Living Room - Bamboo" },
            { 0xcbef938d, "Food Counter - Smoothie" },
            { 0xcbeff420, "Food Counter - Sushi" },
            { 0xcc66ad61, "Piano - Superstar" },
            { 0xcc944729, "Table - End - Cheap 2" },
            { 0xcce4be4f, "Fun House - Track - Right Turn" },
            { 0xcd39bb3a, "Lamp - Floor - Retro 4" },
            { 0xcda8d70a, "Dining Table - Pressed Metal" },
            { 0xcdd887b1, "Fridge - Icebox - Castle" },
            { 0xce10c801, "Door - Music" },
            { 0xce3615a5, "Magic - Spellbook 2( Old )" },
            { 0xce47dd58, "Christmas Cookies" },
            { 0xce4b38cf, "Queue Ropes" },
            { 0xce4d6011, "Door - Wood - Womens" },
            { 0xce6c2041, "Door - Rest - Kitchen" },
            { 0xce6c4713, "Door - Wood - Mens" },
            { 0xceb2f9b8, "Trees - Unleashed - Willow" },
            { 0xcf000516, "Dining Table - Beach Outside" },
            { 0xcf18067e, "Door - Rest X" },
            { 0xcf5a1a2f, "Side Show" },
            { 0xcf5b61d3, "Door - Shop Glass" },
            { 0xcf62b688, "Counter - Lodge" },
            { 0xcf89e47b, "Magic Growth - Beanstalk" },
            { 0xcfd10860, "Trash Can - Carnival" },
            { 0xcfe94d2c, "Door - Rest X 2" },
            { 0xd0a0f269, "Magic Trick Table" },
            { 0xd0a244e4, "Window - Western" },
            { 0xd0b754e0, "Window - Shuttered" },
            { 0xd1180b2a, "Chair - Dining - Tuscan" },
            { 0xd158b046, "Window - Retro 1" },
            { 0xd191aff7, "Computer - Cheap" },
            { 0xd191ff6a, "Computer - Expensive" },
            { 0xd1bc4074, "Toilet - Stall" },
            { 0xd1ef8174, "Balloons - Party" },
            { 0xd2944a33, "Chair - Living Room - Hay Bale" },
            { 0xd2a03993, "VRHelmet" },
            { 0xd2bb9953, "Charades" },
            { 0xd389c839, "Trash Can Inside" },
            { 0xd3a28b82, "Dining Table - Lodge" },
            { 0xd4588282, "Sofa - Loveseat - Pressed Metal" },
            { 0xd45e4bea, "Trash Can - SuperStar" },
            { 0xd4709f75, "Door - Castle 2" },
            { 0xd49346ff, "Stereo - Moderate" },
            { 0xd4bf28f3, "Bed - Single - Antique" },
            { 0xd5a34f0f, "Counter - Vacation - Beach" },
            { 0xd5ca5425, "Shrub - Rose Bush" },
            { 0xd608b270, "Sign - Street - Stop Universal" },
            { 0xd68e73d8, "Shrub - Hedge High" },
            { 0xd735a326, "Window - Circular" },
            { 0xd73d1f82, "Bed - Single - Sleigh" },
            { 0xd7404661, "Pet Post - Cat Scratch" },
            { 0xd84d39ce, "Dining Table - 2 x 1" },
            { 0xd8bb3aa8, "Recliner - Cheap" },
            { 0xd95f52ef, "Table - End - SciFi" },
            { 0xd97681db, "Chair - Dining - Very Expensive" },
            { 0xd985a7ad, "Display Counter Top - Seeds" },
            { 0xd9a73952, "Desk - Cheap" },
            { 0xd9ae1ae5, "Table - End - Roman" },
            { 0xd9bd21cd, "Trees - Barrel Palm " },
            { 0xd9e3bf7b, "Painting - Warhol" },
            { 0xda2b8f79, "Bar - Wet - Neon" },
            { 0xda3bf619, "Magic - Animated Painting 4" },
            { 0xda6132ff, "Lamp - Floor - Animated - Candleabra" },
            { 0xda746262, "Lamp - Floor - Animated - Tiki Torch" },
            { 0xda87b38c, "Exercise Machine Superstar" },
            { 0xda952297, "Trash - Downtown - Aggregate" },
            { 0xdab48923, "Magic - Animated Painting 3" },
            { 0xdac39ea9, "Sculptures - Castle 5" },
            { 0xdb240a0e, "Display Case - Dog Toys" },
            { 0xdb70dbc1, "Table - Dining - Modern" },
            { 0xdbeb6614, "Tub - Moderate" },
            { 0xdc046a4c, "Chair - Connecting" },
            { 0xdc0b2d48, "Painting - Magic - Stage Tapestry3" },
            { 0xdc0bef65, "Shrub - Beach - Century" },
            { 0xdc2dd727, "Painting - Vegas 4" },
            { 0xdcb5a769, "Bed - Single - SciFi" },
            { 0xdce934ea, "Painting - French - Shutters" },
            { 0xdd2cd9be, "Magic - Animated Painting 2" },
            { 0xdd4e8362, "Arcade Game - Seal" },
            { 0xddae91b4, "Magic - Animated Painting 1" },
            { 0xddbb7f1d, "Sofa - Vegas 1" },
            { 0xddecd497, "Exercise Machine" },
            { 0xddf205e3, "Spook Show" },
            { 0xde802250, "Trees - Palm 12 Foot" },
            { 0xdec0fdda, "Sculptures - Scarecrow" },
            { 0xdf77ee12, "Lamp - Table - Cheap" },
            { 0xdf9adf21, "Lamp - Floor - Retro 3" },
            { 0xdfbb8259, "Food Counter - Cheap" },
            { 0xdfd8f77a, "Mirror - Floor - Moderate" },
            { 0xdfde21e4, "Fun House - Track - Straight" },
            { 0xe075d96b, "Dresser - Expensive" },
            { 0xe0df7c0d, "Chair - Dining - Camping" },
            { 0xe0e2da61, "Counter - Resturant - Cheap" },
            { 0xe0f3926b, "Counter - Resturant - Expensive" },
            { 0xe0f69b8f, "Pantry - Modern" },
            { 0xe112d26c, "Pet - Turtle" },
            { 0xe14bfa66, "Table - End - Vegas 1" },
            { 0xe1577be5, "Lamp - Wall - Candle" },
            { 0xe163b26c, "Table - End - Castle 1" },
            { 0xe19c2cff, "Shower - Chrome" },
            { 0xe1b42acd, "Genie Lamp" },
            { 0xe209959f, "SatelliteDish" },
            { 0xe23f9020, "Pet - Bed - Cheap" },
            { 0xe4440b9b, "Cash Register - Downtown - Expensive" },
            { 0xe445b4a6, "Tub - Expensive" },
            { 0xe482787c, "Topiaries - Skyscraper" },
            { 0xe48328e1, "Topiaries - Egg" },
            { 0xe48c2a23, "Topiaries - Llama" },
            { 0xe48d0746, "Topiaries - Pyramid" },
            { 0xe4a3dca2, "Column Arch - Carnival" },
            { 0xe4fb60eb, "Topiaries - Dolphin" },
            { 0xe5293e72, "Plant - Floor - Jade" },
            { 0xe53b581a, "Lamp - Unleashed - Candle - Gold" },
            { 0xe53fa2df, "Lamp - Hula (table) - Ukelele" },
            { 0xe542c148, "Stove - Black Iron" },
            { 0xe56eb692, "Door Double - Stage" },
            { 0xe5c378c5, "Counter - Shop - Expensive" },
            { 0xe5d2ff1f, "Painting - Manowar" },
            { 0xe60e754c, "Dresser - SciFi" },
            { 0xe636aa91, "Door - Executive" },
            { 0xe6888167, "Sofa - Bench - Gypsy" },
            { 0xe69e35c0, "Food Counter - Graveyard Gumbo" },
            { 0xe6e7f18b, "Door - Swiss" },
            { 0xe6fb10f3, "Column Arch - Beach" },
            { 0xe735b451, "Lamp - Ceiling - Superstar 1" },
            { 0xe7938987, "Chair - Dining - Moderate" },
            { 0xe7d374d8, "Sculptures - WagonWheel" },
            { 0xe7d90c21, "Window - Swiss" },
            { 0xe82917b6, "Chair - Dining - Retro 1" },
            { 0xe89c0e48, "SkyDiving Simulator" },
            { 0xe89ff41c, "Column - French" },
            { 0xe8db9140, "Sculptures - Fire Hydrant" },
            { 0xe8e88d38, "DJ Booth" },
            { 0xe93c8663, "Desk - Executive" },
            { 0xe94250e9, "Sofa - Zebra 1" },
            { 0xe9693f98, "Shrub - Gypsy" },
            { 0xe999932c, "Table - End - Lodge" },
            { 0xe9a90c22, "Magic - Spell Making - Charm Maker" },
            { 0xe9f11574, "Chair - Dining - Vegas 1" },
            { 0xe9fc1f39, "Stair - Stone" },
            { 0xea2f9b26, "Phone - Desk Phone" },
            { 0xea50c82f, "Lamp - Unleashed - Candle - Large" },
            { 0xea71c9dc, "Shower - Tiki" },
            { 0xeac3d84f, "Toilet - Portable" },
            { 0xeb686709, "Stove - Cheap" },
            { 0xeb6c0a92, "Desk - Moderate" },
            { 0xeb945bc1, "Clothing Rack - Lingerie" },
            { 0xebbd5efc, "Buffet Table - Vacation" },
            { 0xebdc8225, "Fountain - Roman" },
            { 0xebe254b3, "Toilet - Castle" },
            { 0xec0f97f3, "Clothing Rack - Kids Generic" },
            { 0xec3b09c0, "Sculptures - Surfboard" },
            { 0xec86699b, "Sculptures - Curtain Thing" },
            { 0xeca8d234, "Game - Clown Squirt" },
            { 0xecadc9fd, "NPC - Dragon - Evil/Red" },
            { 0xecf7dde6, "Door - Beach - Room" },
            { 0xed8bea88, "Sofa - Loveseat - Rave3" },
            { 0xede77ffb, "Phone - Pay Booth - MagicTown" },
            { 0xee3f527d, "Lamp - Table - Candle AOL" },
            { 0xee72ed45, "Window - Executive" },
            { 0xee8dc760, "Shack - Rental - Hot Date" },
            { 0xeea7d425, "Alarm - Smoke" },
            { 0xeea95801, "SpaSteamer" },
            { 0xeebceb18, "Dishwasher cheap" },
            { 0xeec5748a, "Chair - Living Room - Barrel" },
            { 0xeeeb7597, "Sofa - Loveseat - Expensive 1" },
            { 0xefb6a70c, "Lamp - Floor - Retro 2" },
            { 0xefba77ca, "Awning - Cheap" },
            { 0xefbdcfbf, "Table - Dining - Deco" },
            { 0xefc4c201, "Magic Growth - Flower" },
            { 0xeff1fdb0, "Sculptures - Retro 1" },
            { 0xf0ce033e, "Balloon Arch" },
            { 0xf0d9ae7f, "Campfire - Single Player" },
            { 0xf126ef7c, "Park Rocker Whale" },
            { 0xf14f637a, "Clothing Booth - Downtown" },
            { 0xf157fd2a, "Haunted House 2" },
            { 0xf15b8010, "Sofa - Loveseat - SEP6 - ArtDeco" },
            { 0xf191c159, "Lamps - Street - Auto" },
            { 0xf1f37c98, "Table - Nightstand - Childs" },
            { 0xf2010049, "Stove - Tuscan" },
            { 0xf219d380, "Food Counter - Cart - Hot Dogs" },
            { 0xf2999f51, "Shrub - Hedge Fame" },
            { 0xf2e97a59, "Door - InteriorMod" },
            { 0xf3b82ddf, "Dining Table - Roman" },
            { 0xf3ca84b4, "Plant - Hanging - Red Flowers" },
            { 0xf3ccccbe, "Plant - Hanging - Fern" },
            { 0xf3f43ce9, "Counter - Kitchen - Tuscan" },
            { 0xf3fab854, "Clothing Rack - Winterwear" },
            { 0xf4126cc4, "Set - Soap Opera" },
            { 0xf416b3ec, "Shower - Aluminum" },
            { 0xf4e5edc9, "Sofa - SEP6 - Mod" },
            { 0xf538d3c7, "Window - Plate Glass" },
            { 0xf53eaf26, "Fence - Privacy" },
            { 0xf56ce72c, "Fence - Studio" },
            { 0xf5b89805, "Recliner - Vacation" },
            { 0xf60db645, "Counter Top Display - Flowers" },
            { 0xf6159793, "Lamp - Garden - Vegas" },
            { 0xf61d92e8, "Coffee Table - Lodge" },
            { 0xf662fbb4, "Chemistry Set" },
            { 0xf718d902, "Bubble Maker" },
            { 0xf73f0765, "Magic - Food Making - Nectar Bar" },
            { 0xf784f104, "Bed - Magic - Canopy" },
            { 0xf78e916d, "Dressing Booth - Vacation - Beach" },
            { 0xf78ed967, "Clothing Booth - Vacation" },
            { 0xf78f37e6, "Lamp - Wall - Exterior" },
            { 0xf79a89c2, "Chair - Living Room - Moderate 1" },
            { 0xf7d7940c, "Painting - Roman - Map" },
            { 0xf7f1f13c, "Window - Beach - Side" },
            { 0xf86f37e4, "Sofa - Loveseat - French" },
            { 0xf88e6aac, "Window - Saloon" },
            { 0xf8c07e5d, "Door - Burgundy Street" },
            { 0xf8e3fc32, "NPC - Unleashed - Skunk" },
            { 0xf8f818e1, "Stair - Spiral - Iron" },
            { 0xf9ca6b20, "Jack-O-Lantern" },
            { 0xf9e11d1c, "Trees - Spellbound - Dead" },
            { 0xfa733902, "VinePlot" },
            { 0xfac41fac, "Sculptures - Movie Camera" },
            { 0xfaf6c024, "Recliner - Expensive" },
            { 0xfbc9619c, "Painting - Magic - Gypsy Tapestry" },
            { 0xfc55fe0a, "Sofa - Bench - Vacation" },
            { 0xfc650ed2, "Booth - Dining - Downtown - Expensive" },
            { 0xfc697613, "NPC - Spellbound - Garden Gnome" },
            { 0xfc8d548b, "Television - Expensive" },
            { 0xfca517aa, "Fence - Lodge Stone" },
            { 0xfd03db16, "Lamp - Garden - Castle" },
            { 0xfd746ad2, "Lamp - Table - Expensive" },
            { 0xfd938797, "Lamp - Floor - Retro 1" },
            { 0xfddca51c, "Bookshelf - Expensive" },
            { 0xfdef7fc2, "Chair - Dining - Roman" },
            { 0xfe1a5de6, "Painting - Superstar - Movie2" },
            { 0xfe7b8edb, "Chairs - Living Room - Booth Tweener - Cheap/Diner" },
            { 0xfe7c3fec, "Lamp - Floor - Moderate" },
            { 0xfea08f34, "Food Counter - Cajun" },
            { 0xfeac70dc, "Sofa - Loveseat - Rave" },
            { 0xfee30120, "Plant - Hanging - Tuscan" },
            { 0xfefda8d6, "Food Counter - Coffee" },
            { 0xff7d2ca4, "Lamp - Wall - Gypsy" },
            { 0xff97ba8f, "Plant - Table - Geranium" },
            { 0xff9f18e0, "Stove - Expensive" },
            { 0xffbb3b82, "Dining Table - Single Tile" },
            { 0xffbe1d84, "Magic - Spell Making - Charm Maker( Kid )" },
            { 0xffc0ea58, "Display Counter Top - Pet Treats" },
        };

        public static readonly Dictionary<string, int> FixtureEligible = new Dictionary<string, int>{
            { "Easel.iff", 1 }, { "ChessTable.iff", 1 }, { "Beds.iff", 5 }, { "Fridges.iff", 3 },
            { "Stoves.iff", 4 }, { "TVs.iff", 3 }, { "ShowerC.iff", 1 }, { "TubC.iff", 1 },
            { "TubM.iff", 1 }, { "Toilets.iff", 2 }, { "Mirrors.iff", 2 }, { "ExerciseMachine.iff", 1 },
            { "BBQ.iff", 1 }, { "Bookcases.iff", 3 }, { "Computers.iff", 4 }, { "Stereos.iff", 3 },
            { "Bars.iff", 1 }, { "HotTub.iff", 1 }, { "Recliners.iff", 2 }, { "ChairsLR1Tile.iff", 4 }
        };
        public static readonly Dictionary<string, uint[]> FixtureGuids = new Dictionary<string, uint[]>{
            { "Easel.iff", new uint[] { 0x9fb223ce } },
            { "ChessTable.iff", new uint[] { 0x94aa32f4 } },
            { "Beds.iff", new uint[] { 0x0e82c943, 0x3089d8d2, 0x6593d291, 0x81179ab3, 0x82e04c5b } },
            { "Fridges.iff", new uint[] { 0x42c078b3, 0x675c18af, 0xb94755df } },
            { "Stoves.iff", new uint[] { 0xad08fdc0, 0xbb626ef3, 0xeb686709, 0xff9f18e0 } },
            { "TVs.iff", new uint[] { 0x5fa381c1, 0xc192dacc, 0xfc8d548b } },
            { "ShowerC.iff", new uint[] { 0x85e02a40 } },
            { "TubC.iff", new uint[] { 0x85e00942 } },
            { "TubM.iff", new uint[] { 0xdbeb6614 } },
            { "Toilets.iff", new uint[] { 0x3c566968, 0x84e0774c } },
            { "Mirrors.iff", new uint[] { 0xc041ed5b, 0xdfd8f77a } },
            { "ExerciseMachine.iff", new uint[] { 0xddecd497 } },
            { "BBQ.iff", new uint[] { 0x253c1a5e } },
            { "Bookcases.iff", new uint[] { 0x5cdc712f, 0x9b3a5cdf, 0xfddca51c } },
            { "Computers.iff", new uint[] { 0x835ba084, 0xafabcc3d, 0xd191aff7, 0xd191ff6a } },
            { "Stereos.iff", new uint[] { 0x46f4e80b, 0xaa2f9672, 0xd49346ff } },
            { "Bars.iff", new uint[] { 0xc2a91fd0 } },
            { "HotTub.iff", new uint[] { 0x8fed54c2 } },
            { "Recliners.iff", new uint[] { 0xd8bb3aa8, 0xfaf6c024 } },
            { "ChairsLR1Tile.iff", new uint[] { 0x22920f55, 0x82e0f146, 0x99120547, 0xf79a89c2 } }
        };
        public static readonly Dictionary<string, long> FixtureChecksum = new Dictionary<string, long>{
            { "Easel.iff", 669812979500L }, { "ChessTable.iff", 1247091066000L }, { "Beds.iff", 8722399543550L },
            { "Fridges.iff", 10155495405300L }, { "Stoves.iff", 6944664166750L }, { "TVs.iff", 15908246122260L },
            { "ShowerC.iff", 1459939395200L }, { "TubC.iff", 1796841729600L }, { "TubM.iff", 5534456094000L },
            { "Toilets.iff", 1883546182800L }, { "Mirrors.iff", 885885352200L }, { "ExerciseMachine.iff", 2606299958500L },
            { "BBQ.iff", 218643565700L }, { "Bookcases.iff", 5263234971650L }, { "Computers.iff", 36481430036789L },
            { "Stereos.iff", 9718095510350L }, { "Bars.iff", 2612690905600L }, { "HotTub.iff", 15695521517000L },
            { "Recliners.iff", 4487941013400L }, { "ChairsLR1Tile.iff", 2759233843050L }
        };

        // Round 49 per-fixture FULL-RAW + BEHAVIORAL-ENTRY IFF-LITERAL canon: generated by
        // tools/iff_objd_behavior.py over game-data/The Sims (20 ORIGINAL fixture objects; IFF-literal
        // mirror of the engine OBJD.RawData surface: v138=104 fields, v136=78 fields).
        // FixtureRawChecksum = sum over eligible OBJDs of sum(fld[i]*(i+1)) over the engine-exposed
        // indices; FixtureBHAVs = per-GUID behavioral entry ids { BHAV_MainID@3, AnimationTableID@11,
        // BHAV_Init@40, BHAV_Place@41, BHAV_UserPickup@42, BHAV_Load@44, BHAV_UserPlace@45 }.
        public static readonly Dictionary<string, long> FixtureRawChecksum = new Dictionary<string, long>{
            { "Easel.iff", 4843189L },
            { "ChessTable.iff", 1147221L },
            { "Beds.iff", 29029770L },
            { "Fridges.iff", 19895210L },
            { "Stoves.iff", 41351866L },
            { "TVs.iff", 24905261L },
            { "ShowerC.iff", 742019L },
            { "TubC.iff", 1322902L },
            { "TubM.iff", 4527705L },
            { "Toilets.iff", 10768071L },
            { "Mirrors.iff", 15721914L },
            { "ExerciseMachine.iff", 9785089L },
            { "BBQ.iff", 972903L },
            { "Bookcases.iff", 26143770L },
            { "Computers.iff", 29943892L },
            { "Stereos.iff", 24044018L },
            { "Bars.iff", 10016904L },
            { "HotTub.iff", 2158891L },
            { "Recliners.iff", 15592877L },
            { "ChairsLR1Tile.iff", 26157493L },
        };
        public static readonly Dictionary<string, Dictionary<uint, int[]>> FixtureBHAVs = new Dictionary<string, Dictionary<uint, int[]>>{
            { "Easel.iff", new Dictionary<uint, int[]> { { 0x9fb223ce, new int[] { 4096, 129, 0, 4098, 0, 0, 4111 } } } },
            { "ChessTable.iff", new Dictionary<uint, int[]> { { 0x94aa32f4, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
            { "Beds.iff", new Dictionary<uint, int[]> { { 0x0e82c943, new int[] { 4096, 129, 0, 0, 0, 0, 0 } }, { 0x3089d8d2, new int[] { 4096, 129, 0, 0, 0, 0, 0 } }, { 0x6593d291, new int[] { 4096, 129, 0, 0, 0, 0, 0 } }, { 0x81179ab3, new int[] { 4096, 129, 0, 0, 0, 0, 0 } }, { 0x82e04c5b, new int[] { 4096, 129, 0, 0, 0, 0, 0 } } } },
            { "Fridges.iff", new Dictionary<uint, int[]> { { 0x42c078b3, new int[] { 4096, 0, 0, 4118, 0, 0, 0 } }, { 0x675c18af, new int[] { 4096, 0, 0, 4117, 0, 0, 0 } }, { 0xb94755df, new int[] { 4096, 0, 0, 4119, 0, 0, 0 } } } },
            { "Stoves.iff", new Dictionary<uint, int[]> { { 0xad08fdc0, new int[] { 0, 0, 0, 0, 0, 0, 0 } }, { 0xbb626ef3, new int[] { 0, 0, 0, 0, 0, 0, 0 } }, { 0xeb686709, new int[] { 0, 0, 0, 0, 0, 0, 0 } }, { 0xff9f18e0, new int[] { 0, 0, 0, 0, 0, 0, 0 } } } },
            { "TVs.iff", new Dictionary<uint, int[]> { { 0x5fa381c1, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xc192dacc, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xfc8d548b, new int[] { 4096, 129, 0, 4148, 0, 0, 4140 } } } },
            { "ShowerC.iff", new Dictionary<uint, int[]> { { 0x85e02a40, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
            { "TubC.iff", new Dictionary<uint, int[]> { { 0x85e00942, new int[] { 4096, 129, 0, 0, 0, 0, 0 } } } },
            { "TubM.iff", new Dictionary<uint, int[]> { { 0xdbeb6614, new int[] { 4096, 129, 0, 0, 0, 0, 0 } } } },
            { "Toilets.iff", new Dictionary<uint, int[]> { { 0x3c566968, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0x84e0774c, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
            { "Mirrors.iff", new Dictionary<uint, int[]> { { 0xc041ed5b, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xdfd8f77a, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
            { "ExerciseMachine.iff", new Dictionary<uint, int[]> { { 0xddecd497, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
            { "BBQ.iff", new Dictionary<uint, int[]> { { 0x253c1a5e, new int[] { 0, 0, 0, 0, 0, 0, 0 } } } },
            { "Bookcases.iff", new Dictionary<uint, int[]> { { 0x5cdc712f, new int[] { 0, 0, 0, 0, 0, 0, 0 } }, { 0x9b3a5cdf, new int[] { 0, 0, 0, 0, 0, 0, 0 } }, { 0xfddca51c, new int[] { 0, 0, 0, 0, 0, 0, 0 } } } },
            { "Computers.iff", new Dictionary<uint, int[]> { { 0x835ba084, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xafabcc3d, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xd191aff7, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xd191ff6a, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
            { "Stereos.iff", new Dictionary<uint, int[]> { { 0x46f4e80b, new int[] { 0, 129, 0, 0, 0, 0, 4141 } }, { 0xaa2f9672, new int[] { 4096, 129, 0, 4113, 0, 0, 4141 } }, { 0xd49346ff, new int[] { 4096, 129, 0, 4113, 0, 0, 4141 } } } },
            { "Bars.iff", new Dictionary<uint, int[]> { { 0xc2a91fd0, new int[] { 0, 0, 0, 0, 0, 0, 0 } } } },
            { "HotTub.iff", new Dictionary<uint, int[]> { { 0x8fed54c2, new int[] { 4096, 0, 0, 4103, 0, 0, 4125 } } } },
            { "Recliners.iff", new Dictionary<uint, int[]> { { 0xd8bb3aa8, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } }, { 0xfaf6c024, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } } } },
            { "ChairsLR1Tile.iff", new Dictionary<uint, int[]> { { 0x22920f55, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0x82e0f146, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0x99120547, new int[] { 0, 129, 0, 0, 0, 0, 0 } }, { 0xf79a89c2, new int[] { 0, 129, 0, 0, 0, 0, 0 } } } },
        };

        // Round 50 whole-serving-corpus BEHAVIORAL-ENTRY IFF-LITERAL canon: generated by
        // tools/iff_objd_corpus_bhavs.py over game-data/The Sims (1114 eligible GUIDs) - for every
        // served buy item, the VM behavioural entry ids (BHAV_MainID@3, AnimationTableID@11,
        // BHAV_Init@40, BHAV_Place@41, BHAV_UserPickup@42, BHAV_Load@44, BHAV_UserPlace@45) must
        // equal the raw OBJD IFF values (IObjectCatalog mount; engine ObjdByGUID index is IFF data).
        public static readonly Dictionary<uint, int[]> CatalogBHAVs = new Dictionary<uint, int[]> {
            { 0x000001ca, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x000c147f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x00230edc, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x00c2b68f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x00c8a5f1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x00dd7908, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x00ddfef9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x00e22e1e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x00e30dc2, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x00fe9850, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x01259297, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0125da9d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0133d442, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x01f02564, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x029c9043, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x02f04067, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x031db391, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x048567b7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x04d86d1f, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x05777d82, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x05d17306, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x05dac986, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x068ead71, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0768a9cb, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x09196e73, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x0924bc1f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x099a621e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x09cc9d1c, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x09d8dcec, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x0a02f059, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0a717442, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x0a8178e9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0a87e8cf, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0ae5df54, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x0aec8c97, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0afae8b0, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0x0b1ad6cc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0b36e878, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0bfdfb2a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x0c04ca02, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x0c0d4574, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0c189d36, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0ce4a17c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0d14b885, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x0d192399, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0d2dde73, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x0d4ce6fd, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x0d94642f, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x0dfd8714, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0e4f2e14, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0x0e82c943, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x0eb692fb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0ecbec1c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0ede95bb, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x0f1aa416, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0f77759b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0f8beda7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0fbb8bf8, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x0fd4ccef, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x0fe5fa64, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x0ffc192a, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x10444309, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x108262be, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x110cf4b1, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x112039a0, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x1181ccb1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x11a92d74, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x11ec66d9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x12a45904, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x12d9e6a3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x135e770f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x14284046, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x144a7815, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x144c3f7c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1468c3a7, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x14ae1e58, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x14ef6729, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x151d17ed, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x15316867, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x15445522, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x154522af, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x15460fca, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x15597b59, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x157021de, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x15717e80, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x1584c383, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x160bb3d1, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x165cd872, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1671a218, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1694348f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x16aed8d8, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x16d3a20d, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x16f14e74, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x171527eb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x17332210, new int[] { 4096, 129, 0, 4100, 0, 0, 4107 } },
            { 0x17579980, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x1772dd4f, new int[] { 4096, 0, 0, 4103, 0, 0, 4125 } },
            { 0x17f06fe1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x17fc774d, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x17fd98f7, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x182e7c24, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x19057c09, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x191b9479, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x1ab85e45, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x1ad5b840, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1b490662, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0x1b833d24, new int[] { 4096, 129, 0, 4148, 0, 0, 4140 } },
            { 0x1bb40a4e, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x1bbdb732, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1c6b6d6a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1cc44f98, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1d772052, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1d8e6e89, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1de172c2, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x1dff3cab, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x1e6d621b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1e743c39, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1e8b9ea7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1e8dd05c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1f15af7e, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0x1f287b65, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x1fac199d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2077e7de, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2130c4e5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x21332b58, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x21e65bbf, new int[] { 4096, 129, 0, 4102, 0, 0, 4115 } },
            { 0x21f954a6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2256ba70, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x2263dceb, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x228e96d2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x22920f55, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x22dd513c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x23941850, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x243f0fa0, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x246f6a23, new int[] { 4096, 129, 0, 4099, 0, 0, 4111 } },
            { 0x24dad5c4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2521000e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x253c1a5e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x254233ad, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x25b82d98, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x25ba0090, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2644d3c7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x26ad0662, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x26ad56ff, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x26bfbb29, new int[] { 4096, 129, 0, 4104, 0, 0, 4104 } },
            { 0x26d24804, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x26e91ef5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2768de7d, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x2796b726, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x27f8f51f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x28975923, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x28a0403e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2935d383, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x294439e2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x294a48ee, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x29cb5a51, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x29eaef1f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2a262d32, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x2a3d8714, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2a7994bf, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2a7d9829, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x2ab64a1d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2abc6e73, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2aca7211, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x2b4502a2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2b49a3f4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2b83b971, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x2b89fd8a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2b956ab9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2ba019c7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2baa0029, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2bb9b580, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2bb9e51d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2bd867fe, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2be7cbba, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0x2c501364, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2c555ea3, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x2c57d9f1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2c5ac343, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2c7dff0e, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x2cfb155a, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x2d0a3de1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2d5f0d6c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2d773bf9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2e10faec, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2e453ea5, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x2e7f37ea, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x2e9fdca5, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x2ecdb068, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x2f014f68, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2f42fc26, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2f86506b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x2f8715d1, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x2f92cd8f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2fc581ad, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x2fcd8a30, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2fdf80dd, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x2ffaa585, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0x3007996e, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x3026a0f4, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x307ddfa6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3089d8d2, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x30bcda26, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x30f4b933, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x31a0e42c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x31edeb80, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3288ed87, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x328ed9e1, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x32f5a529, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0x3313d86b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x334f732f, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x342a5e00, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x34431396, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3480731c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3484c1f7, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x34acf253, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x34f7be98, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x34fe4e17, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x354a1785, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x359e63bd, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x35b26d7f, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x35eaebdc, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x36113a6d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3658d1aa, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3659dca2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3672a8b2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x36f33011, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x36fe2161, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x37186734, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x371e3b21, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x37326795, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3742732c, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x375dd48b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3794bcc5, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x37bfa925, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x38232e50, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x384c487b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x391e258e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x39262c4c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x39319021, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x398cc7a0, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x39ce4936, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x39dd7192, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x39ed3b5a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3aa14a49, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3ac7f3be, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3b0971f3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3bc18581, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3c30cb5d, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x3c52b66d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3c566968, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3c71d2e9, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x3c9ea694, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x3d1f6466, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3d3e7889, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3dbfc043, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3dcc1abe, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x3dea8b15, new int[] { 0, 0, 0, 0, 4099, 0, 0 } },
            { 0x3def46a0, new int[] { 0, 0, 0, 0, 4099, 0, 0 } },
            { 0x3e69a01d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x3e6fda2f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4008dbb7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x404ee381, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x405046ff, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x40c778fd, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4126d70f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x417e691c, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x4188ca2b, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x41a50757, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x41c753c1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x41e1f6f3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x41fdd8d8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4208b8e3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x42197abc, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x42316f67, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x42bcc380, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x42c078b3, new int[] { 4096, 0, 0, 4118, 0, 0, 0 } },
            { 0x431d46c8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4371fde9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x43a116b4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x43a15ebe, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x43b62717, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x440087f3, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x4450e4e0, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x44e8992a, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x4596ee35, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x45caf478, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4699596d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x46ddd450, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x46e807b4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x46ef5577, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x46f4e80b, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0x471a0bf6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x473532d6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x47a99575, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x47e917c4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x47f39f51, new int[] { 4096, 129, 0, 4103, 0, 0, 0 } },
            { 0x481a74ec, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x48a153ad, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x49402e4f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x497a0c3e, new int[] { 0, 0, 0, 0, 4099, 0, 0 } },
            { 0x49f9c16f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4a21d3a7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4a459a5d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4a4d1966, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x4a70df92, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x4a8ae8fb, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4ae4f77c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4af2067e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4b4d30a9, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x4b69ea5a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4b78bf0e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4bd0b785, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x4be4809a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4c103662, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x4c442733, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4c443fa4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4c446f39, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4c659e7d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4c727c5d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4cd0523d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4d6da494, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4ddf498c, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x4deefc68, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4e1ea389, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4e665bf5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4ee8b3cf, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x4ef4ea16, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x4ef80f89, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x4f51d969, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x4f8e1c8b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x4fa133c9, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x4fdec997, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x502367de, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x50b9bc6d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x51474702, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x5152a864, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5156efbc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x515e8635, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5199e127, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x51a767e2, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x51c23b85, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x52112235, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5249b60e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5250b4c1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5295a7a8, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x52ae7a4e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x52c81f9b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x52ed4b7c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x52f6acd7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x52f83bea, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5359bfc1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x536a3e4d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x547f4934, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x54b3b81a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x54c3e0cf, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x54c7f70d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x552a8297, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x55acb9e0, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x55bcbc04, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x56007c4b, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x56276590, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x578f5050, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x57f68405, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x582f5936, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5880d445, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0x58dde126, new int[] { 4096, 0, 0, 4103, 0, 0, 4125 } },
            { 0x59132604, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x59133e93, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x594a89f0, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0x59b200b5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x59c389bc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5a01eb5b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5a12bc70, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x5a1bc4bc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5a5cda09, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5a6feced, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x5a91cee0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5b048a52, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5b088f5b, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x5b182c35, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5b561459, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x5bfb6140, new int[] { 4096, 0, 0, 4103, 0, 0, 4125 } },
            { 0x5c198cbe, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x5cb56b2d, new int[] { 4096, 129, 0, 4097, 0, 0, 4110 } },
            { 0x5cdc712f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5d071f26, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5d0bbdb6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5d39291c, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x5d6b538a, new int[] { 0, 0, 0, 0, 4099, 0, 0 } },
            { 0x5d723615, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5d767e1f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5d7b6688, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5dac929f, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x5e8b157a, new int[] { 4096, 129, 0, 4098, 0, 0, 4111 } },
            { 0x5e9e42fc, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x5edb929d, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x5ede0bc4, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x5f421524, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x5fa381c1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x5faa302a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x5fb93288, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x601b0c9a, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x6065ac2d, new int[] { 4096, 0, 0, 4098, 0, 0, 4105 } },
            { 0x60909779, new int[] { 0, 0, 0, 0, 0, 23, 0 } },
            { 0x60d99178, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x60e23da6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6106a1a7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x615c9169, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x6188f5d7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x61b7982e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x629be21f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x62b6f3c1, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0x62ccb570, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x62f8a7e4, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x6350e96f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x637692ea, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x63a3d7d4, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x645365da, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x645542df, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x64983e4d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x64a10860, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x64f0e2bc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x65274a4f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x65396e67, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6585d104, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6593d291, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x65cb152b, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x66025e42, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x6610c43c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x66131ba8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x66237c5d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x663eaffc, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x66548be0, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x66877bef, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6691c3d8, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x67406e73, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x675c18af, new int[] { 4096, 0, 0, 4117, 0, 0, 0 } },
            { 0x678f09ff, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x6854da61, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x68897ff8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x68af92a3, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x68c11912, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6923633a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6928408e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6954e030, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x69bd37af, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6a3889f8, new int[] { 4096, 129, 0, 4109, 0, 0, 4109 } },
            { 0x6a53de3c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6a55e034, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x6a776e73, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6a8594ad, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6ad59dd3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6aeb3d99, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6b264238, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6bad868b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6bf6d402, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6c1bb706, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6c3bbc3e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6c582f42, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6c7caec2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6c86123e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6c8bd4a3, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x6c9f7100, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6cb7f434, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6cc079c2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6d96dcfa, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x6da8b998, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6dd88e6d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6e0ec37e, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x6e325f22, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x6f2bb8ed, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x6f35df40, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x6f93a84a, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x6fb00726, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x702851b4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x70467b85, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x7051ee96, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x70760da9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x70b0d5f2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x70fc19c3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x718ea9c7, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x722a0fbf, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x724ca405, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7280cbfc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x72a5cbe3, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x72bdbb7d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x72fa6739, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7318c9df, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x7387f3a2, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x738a419a, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x73d6180d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x73eef299, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x74d93e95, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x74e7ffa9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x75ac188c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x75b0b2b7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x765bcec0, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7726e3a6, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x77d26881, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x77d429bb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x77d9944d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7839c094, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x78d7ea4d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x78edcd25, new int[] { 4096, 129, 0, 4110, 0, 0, 0 } },
            { 0x7939d97d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x795e70b6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x79e2bb28, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x79fa7e6b, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x7a56922f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7a8fdbf4, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x7ab03de4, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x7b21053a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7b6c8c63, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7bea0977, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x7bfdcc88, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7c21272e, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x7c24a376, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x7c5e5c6d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7cab54a2, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x7cb11019, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x7d2089b6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7d2995e4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7d4a0f76, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7d9b1a47, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x7e09517e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7e1f25b1, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x7e56b1be, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7e853dea, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7e97444a, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x7ed5d1b6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7ef99b7e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7f0abd26, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x7f1ae4cf, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7f283620, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7f775eee, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x7fab4493, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7fbe5529, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7fd422a4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x7fdf0fc1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x801f7235, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x80858fd1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x81179ab3, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x813a5a6b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x81a87bab, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x81b3a551, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x81c7a5e0, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x81d97262, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0x823a9ee0, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x82e04c5b, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x82e0f146, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x83023da6, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x835ba084, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x83e0af58, new int[] { 4096, 129, 0, 4102, 0, 0, 4115 } },
            { 0x83eb896c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x849207af, new int[] { 4096, 0, 0, 4117, 0, 0, 0 } },
            { 0x84ceb10b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x84e0774c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x852df95d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x855cde99, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8592f25b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x85b424a2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x85b46ca8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x85e00942, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x85e02a40, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x85e4adbe, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x85f1a085, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x8613a26a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x862ccc31, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x86e0bbb5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x877feca1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x87c44d57, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x87c99127, new int[] { 4096, 0, 0, 4119, 0, 0, 0 } },
            { 0x87d00adc, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x880bb75e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x88103f3e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x882480a6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x88417fac, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x8841dbb9, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x88734ce8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x88b6a098, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x88ede461, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x89173b24, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x8942bdc7, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x8a712a85, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8ad987b8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8b04d5e1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8ba56284, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x8bd70015, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8bf2e852, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x8c44ba58, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x8c9350d2, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0x8c9e33c9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8cb4d449, new int[] { 0, 0, 0, 0, 0, 23, 0 } },
            { 0x8d1b6101, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8d4ee7df, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8d70f7b3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8da9ffb4, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0x8dab571f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8e3520bd, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8e880484, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x8e8d9ca9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8eaa68ad, new int[] { 0, 0, 0, 0, 4099, 0, 0 } },
            { 0x8eb8e1a6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8ec5750d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8eec19f7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8f01bdcb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8f033944, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x8f300ef1, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0x8f667b23, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x8fb1bf00, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x8fca5a7b, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0x8fed54c2, new int[] { 4096, 0, 0, 4103, 0, 0, 4125 } },
            { 0x8ff0ca39, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x9082401f, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x90af09f4, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x90c1c7cb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x90c7c9e4, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0x912cbed4, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x91767a36, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x91ed6984, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x921fda07, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9293b59e, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x92c87dc0, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9367e97a, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x93f98c8a, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x94082f6d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x948aed3c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x94aa32f4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x94d2df9d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x94f5cda8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x94fd43dc, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0x952bcac7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x95624034, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x95a59fd2, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x95b09639, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x95d4de33, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x95efe015, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9667a489, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x969bde47, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x96a5928b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x96c58db2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x96cb67ae, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x96e3398d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x97a845c3, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0x984671a9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x98a0a6d6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x98e09eb3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x98e0f8bd, new int[] { 4124, 129, 0, 4098, 0, 0, 4096 } },
            { 0x98e9cfe2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x99120547, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x99400b4a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x994fb8f8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x996de865, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x997beef4, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0x9985a06f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x99e0a9b1, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9a0d8249, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0x9a37fba7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9aa2312f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9ae018bb, new int[] { 4096, 129, 0, 4100, 0, 0, 0 } },
            { 0x9b3340b9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9b3a5cdf, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9b3cdd30, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x9be0e2bf, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x9c223167, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9c541464, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9c59140e, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0x9cb1bb9b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9d4b03a5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9da7b210, new int[] { 4096, 0, 0, 4109, 0, 0, 0 } },
            { 0x9ede28ae, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0x9ee7eb60, new int[] { 0, 0, 0, 0, 0, 23, 0 } },
            { 0x9f12673e, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0x9f3a38b9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9f7295a9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0x9f733d4f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0x9fa148dc, new int[] { 4114, 0, 0, 4096, 0, 0, 0 } },
            { 0x9fb223ce, new int[] { 4096, 129, 0, 4098, 0, 0, 4111 } },
            { 0x9fd45e39, new int[] { 4096, 129, 0, 4097, 0, 0, 4110 } },
            { 0xa01e500d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa022c70c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa02c8857, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa072951f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa08dd989, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa08e0c14, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xa0af4e74, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa0ee756f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa11d2875, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa13f39cf, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa1594e42, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0xa1755ff7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa1bd4c80, new int[] { 4096, 129, 0, 4106, 0, 0, 4104 } },
            { 0xa1d5048a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa1d6fe56, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa22c5865, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa2666952, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa2daa08c, new int[] { 4096, 129, 0, 4100, 0, 0, 4107 } },
            { 0xa2fe803a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa46aed33, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa478d407, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa483d725, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa489640d, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0xa4d8c299, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xa4e75cff, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa5004e8c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa5b9515a, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xa5c0bb1e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa607cc98, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xa67ac823, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xa6a4a1ac, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0xa6aebf5b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa6caf1dd, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xa7340ec3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa77f36ae, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa7a70f83, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0xa805e814, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa82ca08e, new int[] { 4096, 129, 0, 4097, 0, 0, 4110 } },
            { 0xa8ad8714, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa8bdd25b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa8c59f83, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa90a7d01, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xa91403ab, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa91d4963, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xa94c3259, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xa9c05832, new int[] { 4096, 129, 0, 4103, 0, 0, 0 } },
            { 0xa9ec8419, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xaa2f9672, new int[] { 4096, 129, 0, 4113, 0, 0, 4141 } },
            { 0xaa655e9f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xaa75a097, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0xaa86ad2a, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xaae98936, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xaaf447d6, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xab101956, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xab647747, new int[] { 4096, 129, 0, 4097, 0, 0, 4099 } },
            { 0xabe586cd, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xabfc34d3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xabfc4be9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xac262843, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xac66d6ca, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xacee4ba5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xad005811, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xad08fdc0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xad0b9dd6, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xad12e0d0, new int[] { 4096, 129, 0, 4110, 0, 0, 0 } },
            { 0xad2c5018, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xad4cf878, new int[] { 0, 129, 0, 0, 0, 0, 4141 } },
            { 0xad4d0d62, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xad591aac, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xad67a694, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xad67be03, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xad67ee9e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xae12c97a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xae1daf72, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0xae5c99e7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xae6e43d6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xaed36fcb, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xaed3c88a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xaf4a1300, new int[] { 4096, 129, 0, 4100, 0, 0, 0 } },
            { 0xaf85a57c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xafabcc3d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb01c2620, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb0706e2a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb1153e7d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb1230866, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb159c37d, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xb1b176bd, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb21b9510, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb26ab256, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xb31edff8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb346903c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb3760ba8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb407a783, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb46b0e55, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb48a1144, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb499ec29, new int[] { 4096, 129, 0, 4101, 0, 0, 4097 } },
            { 0xb4b333e4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb5215809, new int[] { 0, 0, 0, 0, 4099, 0, 0 } },
            { 0xb5250c97, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb53903aa, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xb56a0aff, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb6236b64, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb62454df, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xb668dd56, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xb78e7f8d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb790a819, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xb796426e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb797465f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb79f3182, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xb7cd406c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb7eaacba, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xb80950cc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb867117e, new int[] { 4096, 129, 0, 4101, 0, 0, 4097 } },
            { 0xb87a8d3f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb89b2451, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xb9020120, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xb94755df, new int[] { 4096, 0, 0, 4119, 0, 0, 0 } },
            { 0xb99d5c6e, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xb9b0c862, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xba67ead2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xba6e27bb, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xba71a336, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xba7e8047, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xba9dc16a, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xbb0846ea, new int[] { 4096, 129, 0, 4097, 0, 0, 4110 } },
            { 0xbb1bcf68, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbb626ef3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbb83a08a, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xbba46c20, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0xbc12cd61, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbc3ba088, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbc4b1c4f, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xbc4b7a23, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xbc537c52, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbc661500, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbcab486a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbcc81aff, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xbd78c75d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbdcf6469, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbde052c5, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xbe1d7003, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbe773f58, new int[] { 4096, 129, 0, 4102, 0, 0, 4103 } },
            { 0xbeb0f165, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xbedd7b26, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbf137195, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbf1b918e, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xbf51b5f3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xbfc58dd0, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc02b406a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc041ed5b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc0e20936, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc0e64804, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0xc192dacc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc1b6da6a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc25fcf86, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc26265b3, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xc2a91fd0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc2f1a050, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc349c14c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc3927fa3, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xc3efd759, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc4370e53, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0xc4387388, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xc48f31a0, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xc4c35536, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xc523d73d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc56562c1, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xc58b5c78, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xc5d8a768, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xc606154b, new int[] { 4096, 129, 0, 4102, 0, 0, 4115 } },
            { 0xc6448ae4, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xc69b6e73, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xc6b2bdd5, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0xc71e43cd, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc73c14a4, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xc74e7da7, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xc7a7215c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc7d177e1, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0xc7d6cf27, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc7efe216, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc884fc9e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc8ebdd42, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc96b93eb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xc99ac74e, new int[] { 4096, 0, 0, 4109, 0, 0, 0 } },
            { 0xca528b1d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xca55ed92, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xcaf38382, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcb386e73, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcb6e77ab, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcb790e58, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xcbc6c317, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcbd2be4e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcbe53119, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcbef938d, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcbeff420, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcc66ad61, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xcc944729, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcce4be4f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcd39bb3a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcda8d70a, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcdd887b1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xce10c801, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xce3615a5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xce47dd58, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xce4b38cf, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xce4d6011, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xce6c2041, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xce6c4713, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xceb2f9b8, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xcf000516, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcf18067e, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xcf5a1a2f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcf5b61d3, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xcf62b688, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xcf89e47b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcfd10860, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xcfe94d2c, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xd0a0f269, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd0a244e4, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xd0b754e0, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xd1180b2a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd158b046, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xd191aff7, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd191ff6a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd1bc4074, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd1ef8174, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd2944a33, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd2a03993, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd2bb9953, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd389c839, new int[] { 4096, 129, 0, 4102, 0, 0, 4115 } },
            { 0xd3a28b82, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd4588282, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xd45e4bea, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd4709f75, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xd49346ff, new int[] { 4096, 129, 0, 4113, 0, 0, 4141 } },
            { 0xd4bf28f3, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xd5a34f0f, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xd5ca5425, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xd608b270, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0xd68e73d8, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xd735a326, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xd73d1f82, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xd7404661, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd84d39ce, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xd8bb3aa8, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0xd95f52ef, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd97681db, new int[] { 4096, 129, 0, 4110, 0, 0, 4110 } },
            { 0xd985a7ad, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd9a73952, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xd9ae1ae5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xd9bd21cd, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xd9e3bf7b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xda2b8f79, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xda3bf619, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xda6132ff, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xda746262, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xda87b38c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xda952297, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdab48923, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdac39ea9, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdb240a0e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdb70dbc1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xdbeb6614, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xdc046a4c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdc0b2d48, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdc0bef65, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdc2dd727, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdcb5a769, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xdce934ea, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdd2cd9be, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdd4e8362, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xddae91b4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xddbb7f1d, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xddecd497, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xddf205e3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xde802250, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xdec0fdda, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdf77ee12, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdf9adf21, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdfbb8259, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xdfd8f77a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xdfde21e4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe075d96b, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xe0df7c0d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe0e2da61, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe0f3926b, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe0f69b8f, new int[] { 4096, 0, 0, 4119, 0, 0, 0 } },
            { 0xe112d26c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe14bfa66, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe1577be5, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe163b26c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe19c2cff, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe1b42acd, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe209959f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe23f9020, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe4440b9b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe445b4a6, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xe482787c, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xe48328e1, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xe48c2a23, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xe48d0746, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xe4a3dca2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe4fb60eb, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xe5293e72, new int[] { 4096, 129, 0, 4100, 0, 0, 0 } },
            { 0xe53b581a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe53fa2df, new int[] { 4096, 129, 0, 4105, 4108, 0, 4113 } },
            { 0xe542c148, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe56eb692, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xe5c378c5, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe5d2ff1f, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0xe60e754c, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xe636aa91, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xe6888167, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xe69e35c0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xe6e7f18b, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xe6fb10f3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe735b451, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe7938987, new int[] { 4096, 129, 0, 4108, 0, 0, 4108 } },
            { 0xe7d374d8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe7d90c21, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xe82917b6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe89c0e48, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xe89ff41c, new int[] { 4096, 129, 0, 4098, 0, 0, 0 } },
            { 0xe8db9140, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe8e88d38, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe93c8663, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xe94250e9, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xe9693f98, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xe999932c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe9a90c22, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe9f11574, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xe9fc1f39, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xea2f9b26, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xea50c82f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xea71c9dc, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xeac3d84f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xeb686709, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xeb6c0a92, new int[] { 4096, 129, 0, 4101, 0, 0, 0 } },
            { 0xeb945bc1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xebbd5efc, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xebdc8225, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xebe254b3, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xec0f97f3, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xec3b09c0, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xec86699b, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xeca8d234, new int[] { 4096, 129, 0, 4106, 0, 0, 0 } },
            { 0xecadc9fd, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0xecf7dde6, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xed8bea88, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xede77ffb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xee3f527d, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xee72ed45, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xee8dc760, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xeea7d425, new int[] { 4096, 129, 0, 4097, 0, 0, 4099 } },
            { 0xeea95801, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0xeebceb18, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xeec5748a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xeeeb7597, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xefb6a70c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xefba77ca, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xefbdcfbf, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xefc4c201, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xeff1fdb0, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf0ce033e, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf0d9ae7f, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf126ef7c, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xf14f637a, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xf157fd2a, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf15b8010, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xf191c159, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf1f37c98, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf2010049, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf219d380, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf2999f51, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xf2e97a59, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xf3b82ddf, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf3ca84b4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf3ccccbe, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf3f43ce9, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf3fab854, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf4126cc4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf416b3ec, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf4e5edc9, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xf538d3c7, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xf53eaf26, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf56ce72c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf5b89805, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0xf60db645, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf6159793, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf61d92e8, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf662fbb4, new int[] { 4096, 129, 0, 4097, 0, 0, 0 } },
            { 0xf718d902, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf73f0765, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf784f104, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xf78e916d, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xf78ed967, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xf78f37e6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf79a89c2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf7d7940c, new int[] { 4096, 129, 0, 4097, 0, 0, 4104 } },
            { 0xf7f1f13c, new int[] { 4096, 0, 0, 4098, 0, 0, 4105 } },
            { 0xf86f37e4, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xf88e6aac, new int[] { 1, 0, 0, 0, 0, 0, 0 } },
            { 0xf8c07e5d, new int[] { 1, 0, 0, 0, 0, 0, 4144 } },
            { 0xf8e3fc32, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0xf8f818e1, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xf9ca6b20, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xf9e11d1c, new int[] { 4096, 0, 0, 4103, 0, 0, 0 } },
            { 0xfa733902, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfac41fac, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfaf6c024, new int[] { 4096, 129, 0, 4105, 0, 0, 0 } },
            { 0xfbc9619c, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfc55fe0a, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xfc650ed2, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xfc697613, new int[] { 0, 128, 0, 0, 0, 0, 0 } },
            { 0xfc8d548b, new int[] { 4096, 129, 0, 4148, 0, 0, 4140 } },
            { 0xfca517aa, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfd03db16, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfd746ad2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfd938797, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfddca51c, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xfdef7fc2, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfe1a5de6, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfe7b8edb, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfe7c3fec, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfea08f34, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xfeac70dc, new int[] { 4096, 129, 0, 0, 0, 0, 0 } },
            { 0xfee30120, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xfefda8d6, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xff7d2ca4, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xff97ba8f, new int[] { 4096, 129, 0, 4110, 0, 0, 0 } },
            { 0xff9f18e0, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xffbb3b82, new int[] { 0, 0, 0, 0, 0, 0, 0 } },
            { 0xffbe1d84, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
            { 0xffc0ea58, new int[] { 0, 129, 0, 0, 0, 0, 0 } },
        };
        public static readonly Dictionary<uint, long> CatalogRawChecksum = new Dictionary<uint, long>{
            { 0x000001ca, 940688L },
            { 0x000c147f, 7115951L },
            { 0x00230edc, 2660349L },
            { 0x00c2b68f, 11339522L },
            { 0x00c8a5f1, 6926650L },
            { 0x00dd7908, 9425028L },
            { 0x00ddfef9, 5460691L },
            { 0x00e22e1e, 7794018L },
            { 0x00e30dc2, 3506874L },
            { 0x00fe9850, 9029853L },
            { 0x01259297, 9765287L },
            { 0x0125da9d, 8744673L },
            { 0x0133d442, 8849586L },
            { 0x01f02564, 7508576L },
            { 0x029c9043, 3284275L },
            { 0x02f04067, 6019426L },
            { 0x031db391, 7390090L },
            { 0x048567b7, 3084412L },
            { 0x04d86d1f, 6319482L },
            { 0x05777d82, 6047224L },
            { 0x05d17306, 8389743L },
            { 0x05dac986, 6630603L },
            { 0x068ead71, 8271059L },
            { 0x0768a9cb, 9822816L },
            { 0x09196e73, 3538152L },
            { 0x0924bc1f, 5568692L },
            { 0x099a621e, 6447883L },
            { 0x09cc9d1c, 12744195L },
            { 0x09d8dcec, 9196282L },
            { 0x0a02f059, 7431003L },
            { 0x0a717442, 12613946L },
            { 0x0a8178e9, 8847083L },
            { 0x0a87e8cf, 8651923L },
            { 0x0ae5df54, 11377480L },
            { 0x0aec8c97, 6453266L },
            { 0x0afae8b0, 10199244L },
            { 0x0b1ad6cc, 7353332L },
            { 0x0b36e878, 7740298L },
            { 0x0bfdfb2a, 10266256L },
            { 0x0c04ca02, 7961812L },
            { 0x0c0d4574, 3489843L },
            { 0x0c189d36, 8519123L },
            { 0x0ce4a17c, 6533547L },
            { 0x0d14b885, 5308754L },
            { 0x0d192399, 8440802L },
            { 0x0d2dde73, 5455395L },
            { 0x0d4ce6fd, 6996906L },
            { 0x0d94642f, 3910146L },
            { 0x0dfd8714, 5977767L },
            { 0x0e4f2e14, 11294453L },
            { 0x0e82c943, 6202491L },
            { 0x0eb692fb, 6510640L },
            { 0x0ecbec1c, 5966163L },
            { 0x0ede95bb, 8028365L },
            { 0x0f1aa416, 6936522L },
            { 0x0f77759b, 6123940L },
            { 0x0f8beda7, 6790379L },
            { 0x0fbb8bf8, 4845684L },
            { 0x0fd4ccef, 7832855L },
            { 0x0fe5fa64, 6097735L },
            { 0x0ffc192a, 11580698L },
            { 0x10444309, 6420927L },
            { 0x108262be, 6799221L },
            { 0x110cf4b1, 5948910L },
            { 0x112039a0, 3658061L },
            { 0x1181ccb1, 9538660L },
            { 0x11a92d74, 7300686L },
            { 0x11ec66d9, 9533799L },
            { 0x12a45904, 3210574L },
            { 0x12d9e6a3, 11561894L },
            { 0x135e770f, 7919368L },
            { 0x14284046, 7132464L },
            { 0x144a7815, 7212059L },
            { 0x144c3f7c, 8870556L },
            { 0x1468c3a7, 5230237L },
            { 0x14ae1e58, 5291039L },
            { 0x14ef6729, 8992577L },
            { 0x151d17ed, 6300372L },
            { 0x15316867, 7650696L },
            { 0x15445522, 8393087L },
            { 0x154522af, 6073480L },
            { 0x15460fca, 5429398L },
            { 0x15597b59, 4932854L },
            { 0x157021de, 4845027L },
            { 0x15717e80, 6485216L },
            { 0x1584c383, 10127674L },
            { 0x160bb3d1, 8280982L },
            { 0x165cd872, 9910116L },
            { 0x1671a218, 10425451L },
            { 0x1694348f, 10335337L },
            { 0x16aed8d8, 8419230L },
            { 0x16d3a20d, 7944398L },
            { 0x16f14e74, 6892264L },
            { 0x171527eb, 8556027L },
            { 0x17332210, 11472344L },
            { 0x17579980, 6441862L },
            { 0x1772dd4f, 6317576L },
            { 0x17f06fe1, 7701823L },
            { 0x17fc774d, 6276614L },
            { 0x17fd98f7, 8559518L },
            { 0x182e7c24, 4862205L },
            { 0x19057c09, 6122400L },
            { 0x191b9479, 10555454L },
            { 0x1ab85e45, 3034369L },
            { 0x1ad5b840, 6841970L },
            { 0x1b490662, 5389982L },
            { 0x1b833d24, 7704091L },
            { 0x1bb40a4e, 6914862L },
            { 0x1bbdb732, 6555777L },
            { 0x1c6b6d6a, 8427475L },
            { 0x1cc44f98, 6512406L },
            { 0x1d772052, 7465858L },
            { 0x1d8e6e89, 5597026L },
            { 0x1de172c2, 5194332L },
            { 0x1dff3cab, 10188072L },
            { 0x1e6d621b, 4612161L },
            { 0x1e743c39, 7369330L },
            { 0x1e8b9ea7, 5125184L },
            { 0x1e8dd05c, 4585177L },
            { 0x1f15af7e, 6170170L },
            { 0x1f287b65, 9854962L },
            { 0x1fac199d, 5234472L },
            { 0x2077e7de, 9453395L },
            { 0x2130c4e5, 7465433L },
            { 0x21332b58, 5357946L },
            { 0x21e65bbf, 8416744L },
            { 0x21f954a6, 8952713L },
            { 0x2256ba70, 1482091L },
            { 0x2263dceb, 8784033L },
            { 0x228e96d2, 8547577L },
            { 0x22920f55, 7507262L },
            { 0x22dd513c, 7554771L },
            { 0x23941850, 1178423L },
            { 0x243f0fa0, 4932979L },
            { 0x246f6a23, 9236053L },
            { 0x24dad5c4, 5753113L },
            { 0x2521000e, 9304593L },
            { 0x253c1a5e, 972903L },
            { 0x254233ad, 6511566L },
            { 0x25b82d98, 11279282L },
            { 0x25ba0090, 3270926L },
            { 0x2644d3c7, 10231418L },
            { 0x26ad0662, 5991735L },
            { 0x26ad56ff, 7754241L },
            { 0x26bfbb29, 12152684L },
            { 0x26d24804, 10824607L },
            { 0x26e91ef5, 8662110L },
            { 0x2768de7d, 6129489L },
            { 0x2796b726, 9279035L },
            { 0x27f8f51f, 6465540L },
            { 0x28975923, 10721185L },
            { 0x28a0403e, 4822648L },
            { 0x2935d383, 8962135L },
            { 0x294439e2, 4382377L },
            { 0x294a48ee, 9193298L },
            { 0x29cb5a51, 5793709L },
            { 0x29eaef1f, 7674291L },
            { 0x2a262d32, 6528783L },
            { 0x2a3d8714, 5998863L },
            { 0x2a7994bf, 6736196L },
            { 0x2a7d9829, 9792088L },
            { 0x2ab64a1d, 6650348L },
            { 0x2abc6e73, 5790431L },
            { 0x2aca7211, 1276076L },
            { 0x2b4502a2, 6881362L },
            { 0x2b49a3f4, 6207973L },
            { 0x2b83b971, 10497021L },
            { 0x2b89fd8a, 7291243L },
            { 0x2b956ab9, 7545417L },
            { 0x2ba019c7, 12294391L },
            { 0x2baa0029, 5404157L },
            { 0x2bb9b580, 6721690L },
            { 0x2bb9e51d, 6961746L },
            { 0x2bd867fe, 7078349L },
            { 0x2be7cbba, 9943416L },
            { 0x2c501364, 5951730L },
            { 0x2c555ea3, 6951040L },
            { 0x2c57d9f1, 6636545L },
            { 0x2c5ac343, 10340011L },
            { 0x2c7dff0e, 4388084L },
            { 0x2cfb155a, 5045765L },
            { 0x2d0a3de1, 9613678L },
            { 0x2d5f0d6c, 4355105L },
            { 0x2d773bf9, 5825913L },
            { 0x2e10faec, 9822167L },
            { 0x2e453ea5, 4982077L },
            { 0x2e7f37ea, 8059400L },
            { 0x2e9fdca5, 6168820L },
            { 0x2ecdb068, 4153241L },
            { 0x2f014f68, 6404097L },
            { 0x2f42fc26, 6727742L },
            { 0x2f86506b, 10284635L },
            { 0x2f8715d1, 1911238L },
            { 0x2f92cd8f, 7837960L },
            { 0x2fc581ad, 10169096L },
            { 0x2fcd8a30, 10707755L },
            { 0x2fdf80dd, 8675619L },
            { 0x2ffaa585, 6206994L },
            { 0x3007996e, 9881891L },
            { 0x3026a0f4, 9556468L },
            { 0x307ddfa6, 7572950L },
            { 0x3089d8d2, 5376267L },
            { 0x30bcda26, 15609019L },
            { 0x30f4b933, 7323182L },
            { 0x31a0e42c, 8191931L },
            { 0x31edeb80, 7979474L },
            { 0x3288ed87, 6648706L },
            { 0x328ed9e1, 6882526L },
            { 0x32f5a529, 9923721L },
            { 0x3313d86b, 6551810L },
            { 0x334f732f, 7745839L },
            { 0x342a5e00, 4227255L },
            { 0x34431396, 8288673L },
            { 0x3480731c, 9107366L },
            { 0x3484c1f7, 5393621L },
            { 0x34acf253, 14255218L },
            { 0x34f7be98, 7201651L },
            { 0x34fe4e17, 7840735L },
            { 0x354a1785, 5677410L },
            { 0x359e63bd, 11506585L },
            { 0x35b26d7f, 7886801L },
            { 0x35eaebdc, 9787733L },
            { 0x36113a6d, 2071000L },
            { 0x3658d1aa, 7139242L },
            { 0x3659dca2, 3367385L },
            { 0x3672a8b2, 7414274L },
            { 0x36f33011, 10258350L },
            { 0x36fe2161, 9341069L },
            { 0x37186734, 6521900L },
            { 0x371e3b21, 11987314L },
            { 0x37326795, 5007389L },
            { 0x3742732c, 4967413L },
            { 0x375dd48b, 8631517L },
            { 0x3794bcc5, 6988444L },
            { 0x37bfa925, 4548715L },
            { 0x38232e50, 6629137L },
            { 0x384c487b, 6403016L },
            { 0x391e258e, 4318617L },
            { 0x39262c4c, 6124737L },
            { 0x39319021, 5672741L },
            { 0x398cc7a0, 5613352L },
            { 0x39ce4936, 7032813L },
            { 0x39dd7192, 4987031L },
            { 0x39ed3b5a, 4049299L },
            { 0x3aa14a49, 2302766L },
            { 0x3ac7f3be, 5003139L },
            { 0x3b0971f3, 8080216L },
            { 0x3bc18581, 4606165L },
            { 0x3c30cb5d, 4845262L },
            { 0x3c52b66d, 6539935L },
            { 0x3c566968, 5247084L },
            { 0x3c71d2e9, 7132778L },
            { 0x3c9ea694, 6730297L },
            { 0x3d1f6466, 5024662L },
            { 0x3d3e7889, 6869861L },
            { 0x3dbfc043, 5827031L },
            { 0x3dcc1abe, 3036519L },
            { 0x3dea8b15, 6831961L },
            { 0x3def46a0, 5342543L },
            { 0x3e69a01d, 7513405L },
            { 0x3e6fda2f, 4390184L },
            { 0x4008dbb7, 11167608L },
            { 0x404ee381, 13008282L },
            { 0x405046ff, 9687373L },
            { 0x40c778fd, 5157342L },
            { 0x4126d70f, 12837451L },
            { 0x417e691c, 11293873L },
            { 0x4188ca2b, 3018989L },
            { 0x41a50757, 5374074L },
            { 0x41c753c1, 10754978L },
            { 0x41e1f6f3, 6697451L },
            { 0x41fdd8d8, 7017401L },
            { 0x4208b8e3, 4808308L },
            { 0x42197abc, 8139887L },
            { 0x42316f67, 11033619L },
            { 0x42bcc380, 6703139L },
            { 0x42c078b3, 6948000L },
            { 0x431d46c8, 9281638L },
            { 0x4371fde9, 7667357L },
            { 0x43a116b4, 7264456L },
            { 0x43a15ebe, 10086385L },
            { 0x43b62717, 7487124L },
            { 0x440087f3, 5555549L },
            { 0x4450e4e0, 10370463L },
            { 0x44e8992a, 8014040L },
            { 0x4596ee35, 3096066L },
            { 0x45caf478, 13659084L },
            { 0x4699596d, 3964211L },
            { 0x46ddd450, 9378090L },
            { 0x46e807b4, 7743777L },
            { 0x46ef5577, 10192342L },
            { 0x46f4e80b, 8656651L },
            { 0x471a0bf6, 8761191L },
            { 0x473532d6, 6727801L },
            { 0x47a99575, 9339800L },
            { 0x47e917c4, 8915096L },
            { 0x47f39f51, 1928894L },
            { 0x481a74ec, 5561847L },
            { 0x48a153ad, 6622170L },
            { 0x49402e4f, 2045030L },
            { 0x497a0c3e, 5944782L },
            { 0x49f9c16f, 9469242L },
            { 0x4a21d3a7, 4848212L },
            { 0x4a459a5d, 11782851L },
            { 0x4a4d1966, 5451713L },
            { 0x4a70df92, 5294689L },
            { 0x4a8ae8fb, 5276253L },
            { 0x4ae4f77c, 5362841L },
            { 0x4af2067e, 6409467L },
            { 0x4b4d30a9, 9697379L },
            { 0x4b69ea5a, 4423109L },
            { 0x4b78bf0e, 11171723L },
            { 0x4bd0b785, 7426495L },
            { 0x4be4809a, 10987823L },
            { 0x4c103662, 6566294L },
            { 0x4c442733, 8868058L },
            { 0x4c443fa4, 9484557L },
            { 0x4c446f39, 11362328L },
            { 0x4c659e7d, 6662934L },
            { 0x4c727c5d, 6835336L },
            { 0x4cd0523d, 9614279L },
            { 0x4d6da494, 9374463L },
            { 0x4ddf498c, 14013690L },
            { 0x4deefc68, 2947382L },
            { 0x4e1ea389, 8248215L },
            { 0x4e665bf5, 8510956L },
            { 0x4ee8b3cf, 8278935L },
            { 0x4ef4ea16, 1874538L },
            { 0x4ef80f89, 7408771L },
            { 0x4f51d969, 9091234L },
            { 0x4f8e1c8b, 6263055L },
            { 0x4fa133c9, 5138331L },
            { 0x4fdec997, 6896096L },
            { 0x502367de, 7080217L },
            { 0x50b9bc6d, 8388337L },
            { 0x51474702, 4766561L },
            { 0x5152a864, 7563859L },
            { 0x5156efbc, 8594091L },
            { 0x515e8635, 8427077L },
            { 0x5199e127, 1781442L },
            { 0x51a767e2, 6868612L },
            { 0x51c23b85, 11132672L },
            { 0x52112235, 5830661L },
            { 0x5249b60e, 13055993L },
            { 0x5250b4c1, 5333826L },
            { 0x5295a7a8, 6387443L },
            { 0x52ae7a4e, 8784760L },
            { 0x52c81f9b, 10074340L },
            { 0x52ed4b7c, 6445056L },
            { 0x52f6acd7, 7434493L },
            { 0x52f83bea, 8056479L },
            { 0x5359bfc1, 7777697L },
            { 0x536a3e4d, 9072396L },
            { 0x547f4934, 12787988L },
            { 0x54b3b81a, 7159697L },
            { 0x54c3e0cf, 10170828L },
            { 0x54c7f70d, 8105486L },
            { 0x552a8297, 6169102L },
            { 0x55acb9e0, 6416379L },
            { 0x55bcbc04, 5880899L },
            { 0x56007c4b, 6889188L },
            { 0x56276590, 9888676L },
            { 0x578f5050, 9885364L },
            { 0x57f68405, 10001191L },
            { 0x582f5936, 7556877L },
            { 0x5880d445, 6760693L },
            { 0x58dde126, 6556717L },
            { 0x59132604, 6564072L },
            { 0x59133e93, 2813965L },
            { 0x594a89f0, 5879975L },
            { 0x59b200b5, 7048075L },
            { 0x59c389bc, 11760466L },
            { 0x5a01eb5b, 11247606L },
            { 0x5a12bc70, 11449104L },
            { 0x5a1bc4bc, 9529992L },
            { 0x5a5cda09, 5588269L },
            { 0x5a6feced, 6955625L },
            { 0x5a91cee0, 5194019L },
            { 0x5b048a52, 5864050L },
            { 0x5b088f5b, 10015931L },
            { 0x5b182c35, 5159239L },
            { 0x5b561459, 5176456L },
            { 0x5bfb6140, 7644173L },
            { 0x5c198cbe, 5812344L },
            { 0x5cb56b2d, 11779909L },
            { 0x5cdc712f, 11991055L },
            { 0x5d071f26, 3250527L },
            { 0x5d0bbdb6, 7782587L },
            { 0x5d39291c, 5072458L },
            { 0x5d6b538a, 6701293L },
            { 0x5d723615, 10022588L },
            { 0x5d767e1f, 8228276L },
            { 0x5d7b6688, 7555821L },
            { 0x5dac929f, 9835492L },
            { 0x5e8b157a, 8936122L },
            { 0x5e9e42fc, 3559374L },
            { 0x5edb929d, 8535194L },
            { 0x5ede0bc4, 3827089L },
            { 0x5f421524, 3070116L },
            { 0x5fa381c1, 8290416L },
            { 0x5faa302a, 6043032L },
            { 0x5fb93288, 8426302L },
            { 0x601b0c9a, 4978979L },
            { 0x6065ac2d, 15375395L },
            { 0x60909779, 12705177L },
            { 0x60d99178, 6410098L },
            { 0x60e23da6, 3241606L },
            { 0x6106a1a7, 9875778L },
            { 0x615c9169, 4872750L },
            { 0x6188f5d7, 7704991L },
            { 0x61b7982e, 5297278L },
            { 0x629be21f, 2880857L },
            { 0x62b6f3c1, 9821320L },
            { 0x62ccb570, 8476615L },
            { 0x62f8a7e4, 8996147L },
            { 0x6350e96f, 4594730L },
            { 0x637692ea, 9361100L },
            { 0x63a3d7d4, 7566565L },
            { 0x645365da, 10739253L },
            { 0x645542df, 7270208L },
            { 0x64983e4d, 10226743L },
            { 0x64a10860, 3244868L },
            { 0x64f0e2bc, 4748545L },
            { 0x65274a4f, 11446567L },
            { 0x65396e67, 8306758L },
            { 0x6585d104, 6584174L },
            { 0x6593d291, 5641331L },
            { 0x65cb152b, 10468586L },
            { 0x66025e42, 8131358L },
            { 0x6610c43c, 5968034L },
            { 0x66131ba8, 4861063L },
            { 0x66237c5d, 10240206L },
            { 0x663eaffc, 6589601L },
            { 0x66548be0, 8294131L },
            { 0x66877bef, 10214460L },
            { 0x6691c3d8, 10320240L },
            { 0x67406e73, 7420064L },
            { 0x675c18af, 5644214L },
            { 0x678f09ff, 10737594L },
            { 0x6854da61, 8076534L },
            { 0x68897ff8, 5035450L },
            { 0x68af92a3, 7277532L },
            { 0x68c11912, 8746070L },
            { 0x6923633a, 7671599L },
            { 0x6928408e, 8942991L },
            { 0x6954e030, 11508903L },
            { 0x69bd37af, 7021404L },
            { 0x6a3889f8, 12252984L },
            { 0x6a53de3c, 9257287L },
            { 0x6a55e034, 9849947L },
            { 0x6a776e73, 6260293L },
            { 0x6a8594ad, 7053126L },
            { 0x6ad59dd3, 8752696L },
            { 0x6aeb3d99, 6402792L },
            { 0x6b264238, 10098220L },
            { 0x6bad868b, 8455288L },
            { 0x6bf6d402, 11322714L },
            { 0x6c1bb706, 4708814L },
            { 0x6c3bbc3e, 6732272L },
            { 0x6c582f42, 6044553L },
            { 0x6c7caec2, 8090108L },
            { 0x6c86123e, 5338064L },
            { 0x6c8bd4a3, 7962795L },
            { 0x6c9f7100, 4387714L },
            { 0x6cb7f434, 9885760L },
            { 0x6cc079c2, 10162208L },
            { 0x6d96dcfa, 5931394L },
            { 0x6da8b998, 7837440L },
            { 0x6dd88e6d, 7719645L },
            { 0x6e0ec37e, 9255012L },
            { 0x6e325f22, 8814657L },
            { 0x6f2bb8ed, 5684114L },
            { 0x6f35df40, 6236750L },
            { 0x6f93a84a, 11410897L },
            { 0x6fb00726, 6580467L },
            { 0x702851b4, 4993911L },
            { 0x70467b85, 10301464L },
            { 0x7051ee96, 4281984L },
            { 0x70760da9, 8409794L },
            { 0x70b0d5f2, 9735975L },
            { 0x70fc19c3, 5387701L },
            { 0x718ea9c7, 7018225L },
            { 0x722a0fbf, 11328900L },
            { 0x724ca405, 6393286L },
            { 0x7280cbfc, 9695945L },
            { 0x72a5cbe3, 12795050L },
            { 0x72bdbb7d, 7652531L },
            { 0x72fa6739, 7301428L },
            { 0x7318c9df, 3195633L },
            { 0x7387f3a2, 8141820L },
            { 0x738a419a, 5830811L },
            { 0x73d6180d, 6016747L },
            { 0x73eef299, 4937387L },
            { 0x74d93e95, 8961341L },
            { 0x74e7ffa9, 9528498L },
            { 0x75ac188c, 6660062L },
            { 0x75b0b2b7, 9484484L },
            { 0x765bcec0, 8151274L },
            { 0x7726e3a6, 8603483L },
            { 0x77d26881, 6446850L },
            { 0x77d429bb, 11557753L },
            { 0x77d9944d, 6876506L },
            { 0x7839c094, 13057672L },
            { 0x78d7ea4d, 9065504L },
            { 0x78edcd25, 12213570L },
            { 0x7939d97d, 7989660L },
            { 0x795e70b6, 6297818L },
            { 0x79e2bb28, 6119341L },
            { 0x79fa7e6b, 5418983L },
            { 0x7a56922f, 6953154L },
            { 0x7a8fdbf4, 3138309L },
            { 0x7ab03de4, 4984585L },
            { 0x7b21053a, 8604553L },
            { 0x7b6c8c63, 9894244L },
            { 0x7bea0977, 5855662L },
            { 0x7bfdcc88, 9773314L },
            { 0x7c21272e, 6952513L },
            { 0x7c24a376, 12095871L },
            { 0x7c5e5c6d, 7673883L },
            { 0x7cab54a2, 5838878L },
            { 0x7cb11019, 4647319L },
            { 0x7d2089b6, 8808798L },
            { 0x7d2995e4, 5889791L },
            { 0x7d4a0f76, 1741824L },
            { 0x7d9b1a47, 7282105L },
            { 0x7e09517e, 7450848L },
            { 0x7e1f25b1, 3907660L },
            { 0x7e56b1be, 5995031L },
            { 0x7e853dea, 7567166L },
            { 0x7e97444a, 8293109L },
            { 0x7ed5d1b6, 15471292L },
            { 0x7ef99b7e, 5493489L },
            { 0x7f0abd26, 8692178L },
            { 0x7f1ae4cf, 6665080L },
            { 0x7f283620, 5175894L },
            { 0x7f775eee, 8986052L },
            { 0x7fab4493, 6831363L },
            { 0x7fbe5529, 11010683L },
            { 0x7fd422a4, 8659654L },
            { 0x7fdf0fc1, 12563479L },
            { 0x801f7235, 12487279L },
            { 0x80858fd1, 3565887L },
            { 0x81179ab3, 5579320L },
            { 0x813a5a6b, 11541959L },
            { 0x81a87bab, 6887340L },
            { 0x81b3a551, 3218127L },
            { 0x81c7a5e0, 6191110L },
            { 0x81d97262, 8720234L },
            { 0x823a9ee0, 8366943L },
            { 0x82e04c5b, 6230361L },
            { 0x82e0f146, 8325396L },
            { 0x83023da6, 7272535L },
            { 0x835ba084, 6414624L },
            { 0x83e0af58, 3573200L },
            { 0x83eb896c, 6578644L },
            { 0x849207af, 6826482L },
            { 0x84ceb10b, 9710950L },
            { 0x84e0774c, 5520987L },
            { 0x852df95d, 5942255L },
            { 0x855cde99, 8058685L },
            { 0x8592f25b, 8024913L },
            { 0x85b424a2, 9505039L },
            { 0x85b46ca8, 8635175L },
            { 0x85e00942, 1322902L },
            { 0x85e02a40, 742019L },
            { 0x85e4adbe, 11443389L },
            { 0x85f1a085, 10464025L },
            { 0x8613a26a, 10662408L },
            { 0x862ccc31, 7521358L },
            { 0x86e0bbb5, 4868280L },
            { 0x877feca1, 8579208L },
            { 0x87c44d57, 9613050L },
            { 0x87c99127, 9793176L },
            { 0x87d00adc, 4139106L },
            { 0x880bb75e, 5735305L },
            { 0x88103f3e, 3245360L },
            { 0x882480a6, 12232681L },
            { 0x88417fac, 5273637L },
            { 0x8841dbb9, 10199169L },
            { 0x88734ce8, 12122749L },
            { 0x88b6a098, 6743924L },
            { 0x88ede461, 8820475L },
            { 0x89173b24, 9919702L },
            { 0x8942bdc7, 5805291L },
            { 0x8a712a85, 7078338L },
            { 0x8ad987b8, 9025714L },
            { 0x8b04d5e1, 5358644L },
            { 0x8ba56284, 7060191L },
            { 0x8bd70015, 9055015L },
            { 0x8bf2e852, 7519369L },
            { 0x8c44ba58, 5651517L },
            { 0x8c9350d2, 10725785L },
            { 0x8c9e33c9, 7919893L },
            { 0x8cb4d449, 10465235L },
            { 0x8d1b6101, 9587495L },
            { 0x8d4ee7df, 7270202L },
            { 0x8d70f7b3, 8136734L },
            { 0x8da9ffb4, 9682750L },
            { 0x8dab571f, 10769734L },
            { 0x8e3520bd, 3137650L },
            { 0x8e880484, 7119544L },
            { 0x8e8d9ca9, 7118307L },
            { 0x8eaa68ad, 5259488L },
            { 0x8eb8e1a6, 8332942L },
            { 0x8ec5750d, 6384568L },
            { 0x8eec19f7, 9812566L },
            { 0x8f01bdcb, 5547755L },
            { 0x8f033944, 3525706L },
            { 0x8f300ef1, 10095326L },
            { 0x8f667b23, 6785224L },
            { 0x8fb1bf00, 3649888L },
            { 0x8fca5a7b, 8444325L },
            { 0x8fed54c2, 2158891L },
            { 0x8ff0ca39, 3300179L },
            { 0x9082401f, 7093999L },
            { 0x90af09f4, 6510666L },
            { 0x90c1c7cb, 4910771L },
            { 0x90c7c9e4, 7129735L },
            { 0x912cbed4, 7706653L },
            { 0x91767a36, 6428458L },
            { 0x91ed6984, 9028008L },
            { 0x921fda07, 7491825L },
            { 0x9293b59e, 11283971L },
            { 0x92c87dc0, 6661665L },
            { 0x9367e97a, 10165993L },
            { 0x93f98c8a, 13094584L },
            { 0x94082f6d, 7647417L },
            { 0x948aed3c, 7010825L },
            { 0x94aa32f4, 1147221L },
            { 0x94d2df9d, 4328660L },
            { 0x94f5cda8, 6696876L },
            { 0x94fd43dc, 8484590L },
            { 0x952bcac7, 3238587L },
            { 0x95624034, 7569022L },
            { 0x95a59fd2, 5849960L },
            { 0x95b09639, 10367586L },
            { 0x95d4de33, 11967098L },
            { 0x95efe015, 7231128L },
            { 0x9667a489, 6599640L },
            { 0x969bde47, 7575865L },
            { 0x96a5928b, 6600946L },
            { 0x96c58db2, 9165479L },
            { 0x96cb67ae, 6579514L },
            { 0x96e3398d, 9534100L },
            { 0x97a845c3, 12403942L },
            { 0x984671a9, 7607984L },
            { 0x98a0a6d6, 2731528L },
            { 0x98e09eb3, 1249986L },
            { 0x98e0f8bd, 6188203L },
            { 0x98e9cfe2, 12696468L },
            { 0x99120547, 1920564L },
            { 0x99400b4a, 10718466L },
            { 0x994fb8f8, 2482825L },
            { 0x996de865, 8639881L },
            { 0x997beef4, 8884075L },
            { 0x9985a06f, 8173843L },
            { 0x99e0a9b1, 6859993L },
            { 0x9a0d8249, 4507848L },
            { 0x9a37fba7, 7678410L },
            { 0x9aa2312f, 10766281L },
            { 0x9ae018bb, 4215415L },
            { 0x9b3340b9, 9391044L },
            { 0x9b3a5cdf, 6195785L },
            { 0x9b3cdd30, 8708749L },
            { 0x9be0e2bf, 2127642L },
            { 0x9c223167, 3772814L },
            { 0x9c541464, 6916071L },
            { 0x9c59140e, 6170657L },
            { 0x9cb1bb9b, 10564147L },
            { 0x9d4b03a5, 4621834L },
            { 0x9da7b210, 10898711L },
            { 0x9ede28ae, 4918057L },
            { 0x9ee7eb60, 13094582L },
            { 0x9f12673e, 3645812L },
            { 0x9f3a38b9, 7504570L },
            { 0x9f7295a9, 13671372L },
            { 0x9f733d4f, 6908379L },
            { 0x9fa148dc, 4975252L },
            { 0x9fb223ce, 4843189L },
            { 0x9fd45e39, 11050195L },
            { 0xa01e500d, 10043145L },
            { 0xa022c70c, 9778746L },
            { 0xa02c8857, 9737704L },
            { 0xa072951f, 3550102L },
            { 0xa08dd989, 14189049L },
            { 0xa08e0c14, 7839274L },
            { 0xa0af4e74, 5573217L },
            { 0xa0ee756f, 3228102L },
            { 0xa11d2875, 12124455L },
            { 0xa13f39cf, 10868720L },
            { 0xa1594e42, 9734314L },
            { 0xa1755ff7, 12544462L },
            { 0xa1bd4c80, 2622547L },
            { 0xa1d5048a, 8681403L },
            { 0xa1d6fe56, 11066191L },
            { 0xa22c5865, 5994458L },
            { 0xa2666952, 9431379L },
            { 0xa2daa08c, 10318148L },
            { 0xa2fe803a, 4622031L },
            { 0xa46aed33, 11392789L },
            { 0xa478d407, 4086526L },
            { 0xa483d725, 5066175L },
            { 0xa489640d, 5288908L },
            { 0xa4d8c299, 5177406L },
            { 0xa4e75cff, 6741248L },
            { 0xa5004e8c, 13747202L },
            { 0xa5b9515a, 4735132L },
            { 0xa5c0bb1e, 4967324L },
            { 0xa607cc98, 6201464L },
            { 0xa67ac823, 10137040L },
            { 0xa6a4a1ac, 6515339L },
            { 0xa6aebf5b, 6716115L },
            { 0xa6caf1dd, 8506533L },
            { 0xa7340ec3, 9512166L },
            { 0xa77f36ae, 3719073L },
            { 0xa7a70f83, 9166653L },
            { 0xa805e814, 4160836L },
            { 0xa82ca08e, 5394608L },
            { 0xa8ad8714, 7410733L },
            { 0xa8bdd25b, 9990662L },
            { 0xa8c59f83, 12775767L },
            { 0xa90a7d01, 6646633L },
            { 0xa91403ab, 8900553L },
            { 0xa91d4963, 9104093L },
            { 0xa94c3259, 8910541L },
            { 0xa9c05832, 6192259L },
            { 0xa9ec8419, 6304978L },
            { 0xaa2f9672, 7626242L },
            { 0xaa655e9f, 9688308L },
            { 0xaa75a097, 9319811L },
            { 0xaa86ad2a, 9364818L },
            { 0xaae98936, 8894044L },
            { 0xaaf447d6, 6991439L },
            { 0xab101956, 6826449L },
            { 0xab647747, 13178973L },
            { 0xabe586cd, 9044875L },
            { 0xabfc34d3, 4085313L },
            { 0xabfc4be9, 2203084L },
            { 0xac262843, 7474170L },
            { 0xac66d6ca, 14810798L },
            { 0xacee4ba5, 6037391L },
            { 0xad005811, 11227528L },
            { 0xad08fdc0, 10403305L },
            { 0xad0b9dd6, 10982749L },
            { 0xad12e0d0, 8869906L },
            { 0xad2c5018, 6334205L },
            { 0xad4cf878, 6553955L },
            { 0xad4d0d62, 4440630L },
            { 0xad591aac, 5286367L },
            { 0xad67a694, 3911679L },
            { 0xad67be03, 5212468L },
            { 0xad67ee9e, 4484113L },
            { 0xae12c97a, 8424336L },
            { 0xae1daf72, 9004383L },
            { 0xae5c99e7, 7418073L },
            { 0xae6e43d6, 8327970L },
            { 0xaed36fcb, 9812866L },
            { 0xaed3c88a, 10420014L },
            { 0xaf4a1300, 4263261L },
            { 0xaf85a57c, 10377844L },
            { 0xafabcc3d, 9953940L },
            { 0xb01c2620, 8247000L },
            { 0xb0706e2a, 7213533L },
            { 0xb1153e7d, 6739222L },
            { 0xb1230866, 7817283L },
            { 0xb159c37d, 9760424L },
            { 0xb1b176bd, 7662126L },
            { 0xb21b9510, 5810696L },
            { 0xb26ab256, 5420814L },
            { 0xb31edff8, 9829451L },
            { 0xb346903c, 3399291L },
            { 0xb3760ba8, 9033487L },
            { 0xb407a783, 7295884L },
            { 0xb46b0e55, 7414901L },
            { 0xb48a1144, 6688108L },
            { 0xb499ec29, 6213711L },
            { 0xb4b333e4, 3273712L },
            { 0xb5215809, 9732768L },
            { 0xb5250c97, 11400819L },
            { 0xb53903aa, 2953552L },
            { 0xb56a0aff, 8080007L },
            { 0xb6236b64, 8287074L },
            { 0xb62454df, 5756802L },
            { 0xb668dd56, 2356678L },
            { 0xb78e7f8d, 6446284L },
            { 0xb790a819, 3224611L },
            { 0xb796426e, 6881971L },
            { 0xb797465f, 6376467L },
            { 0xb79f3182, 9662352L },
            { 0xb7cd406c, 8748752L },
            { 0xb7eaacba, 7965814L },
            { 0xb80950cc, 7456911L },
            { 0xb867117e, 5508964L },
            { 0xb87a8d3f, 12163920L },
            { 0xb89b2451, 3807065L },
            { 0xb9020120, 6229951L },
            { 0xb94755df, 7302996L },
            { 0xb99d5c6e, 6238858L },
            { 0xb9b0c862, 9873771L },
            { 0xba67ead2, 7293925L },
            { 0xba6e27bb, 4258224L },
            { 0xba71a336, 3603931L },
            { 0xba7e8047, 6992112L },
            { 0xba9dc16a, 8268570L },
            { 0xbb0846ea, 9783322L },
            { 0xbb1bcf68, 8969818L },
            { 0xbb626ef3, 9984592L },
            { 0xbb83a08a, 11348389L },
            { 0xbba46c20, 8542259L },
            { 0xbc12cd61, 7915873L },
            { 0xbc3ba088, 9391841L },
            { 0xbc4b1c4f, 6285196L },
            { 0xbc4b7a23, 7427448L },
            { 0xbc537c52, 5001268L },
            { 0xbc661500, 7556828L },
            { 0xbcab486a, 10956037L },
            { 0xbcc81aff, 7513614L },
            { 0xbd78c75d, 9862526L },
            { 0xbdcf6469, 1784873L },
            { 0xbde052c5, 6051869L },
            { 0xbe1d7003, 4653623L },
            { 0xbe773f58, 6720044L },
            { 0xbeb0f165, 7737257L },
            { 0xbedd7b26, 5107862L },
            { 0xbf137195, 10346091L },
            { 0xbf1b918e, 7547031L },
            { 0xbf51b5f3, 5795115L },
            { 0xbfc58dd0, 7472018L },
            { 0xc02b406a, 9110564L },
            { 0xc041ed5b, 13681784L },
            { 0xc0e20936, 7570873L },
            { 0xc0e64804, 10885458L },
            { 0xc192dacc, 10116782L },
            { 0xc1b6da6a, 8672321L },
            { 0xc25fcf86, 6069053L },
            { 0xc26265b3, 5885681L },
            { 0xc2a91fd0, 10016904L },
            { 0xc2f1a050, 9739021L },
            { 0xc349c14c, 9114552L },
            { 0xc3927fa3, 2099454L },
            { 0xc3efd759, 6560491L },
            { 0xc4370e53, 8750381L },
            { 0xc4387388, 6424933L },
            { 0xc48f31a0, 5214265L },
            { 0xc4c35536, 4194299L },
            { 0xc523d73d, 6429341L },
            { 0xc56562c1, 7570644L },
            { 0xc58b5c78, 5573265L },
            { 0xc5d8a768, 5917843L },
            { 0xc606154b, 8796272L },
            { 0xc6448ae4, 2821503L },
            { 0xc69b6e73, 4743936L },
            { 0xc6b2bdd5, 11565387L },
            { 0xc71e43cd, 9289645L },
            { 0xc73c14a4, 5337341L },
            { 0xc74e7da7, 11585422L },
            { 0xc7a7215c, 7296740L },
            { 0xc7d177e1, 9552593L },
            { 0xc7d6cf27, 10671217L },
            { 0xc7efe216, 9932918L },
            { 0xc884fc9e, 9637003L },
            { 0xc8ebdd42, 2214232L },
            { 0xc96b93eb, 12123604L },
            { 0xc99ac74e, 8062111L },
            { 0xca528b1d, 6463522L },
            { 0xca55ed92, 7369953L },
            { 0xcaf38382, 11601832L },
            { 0xcb386e73, 10310904L },
            { 0xcb6e77ab, 6651054L },
            { 0xcb790e58, 3077559L },
            { 0xcbc6c317, 5364978L },
            { 0xcbd2be4e, 6964010L },
            { 0xcbe53119, 6195170L },
            { 0xcbef938d, 9698293L },
            { 0xcbeff420, 6950435L },
            { 0xcc66ad61, 4261331L },
            { 0xcc944729, 7797678L },
            { 0xcce4be4f, 7408710L },
            { 0xcd39bb3a, 8479771L },
            { 0xcda8d70a, 9310254L },
            { 0xcdd887b1, 7355312L },
            { 0xce10c801, 7940389L },
            { 0xce3615a5, 6218624L },
            { 0xce47dd58, 11211378L },
            { 0xce4b38cf, 11631946L },
            { 0xce4d6011, 4526770L },
            { 0xce6c2041, 3562620L },
            { 0xce6c4713, 6772249L },
            { 0xceb2f9b8, 11906612L },
            { 0xcf000516, 1873444L },
            { 0xcf18067e, 5444084L },
            { 0xcf5a1a2f, 9638620L },
            { 0xcf5b61d3, 6170149L },
            { 0xcf62b688, 12999886L },
            { 0xcf89e47b, 5086010L },
            { 0xcfd10860, 6792663L },
            { 0xcfe94d2c, 3295684L },
            { 0xd0a0f269, 8066495L },
            { 0xd0a244e4, 3268842L },
            { 0xd0b754e0, 6728517L },
            { 0xd1180b2a, 8896097L },
            { 0xd158b046, 5571715L },
            { 0xd191aff7, 5791420L },
            { 0xd191ff6a, 7783908L },
            { 0xd1bc4074, 6866044L },
            { 0xd1ef8174, 9217288L },
            { 0xd2944a33, 7781723L },
            { 0xd2a03993, 6858788L },
            { 0xd2bb9953, 9776640L },
            { 0xd389c839, 9439069L },
            { 0xd3a28b82, 7378490L },
            { 0xd4588282, 7206695L },
            { 0xd45e4bea, 10200642L },
            { 0xd4709f75, 8493151L },
            { 0xd49346ff, 7761125L },
            { 0xd4bf28f3, 6337928L },
            { 0xd5a34f0f, 11729330L },
            { 0xd5ca5425, 3787530L },
            { 0xd608b270, 8890237L },
            { 0xd68e73d8, 11297731L },
            { 0xd735a326, 5962610L },
            { 0xd73d1f82, 5479585L },
            { 0xd7404661, 12611279L },
            { 0xd84d39ce, 6481287L },
            { 0xd8bb3aa8, 7859048L },
            { 0xd95f52ef, 6792261L },
            { 0xd97681db, 12649422L },
            { 0xd985a7ad, 9482231L },
            { 0xd9a73952, 4237876L },
            { 0xd9ae1ae5, 7704128L },
            { 0xd9bd21cd, 11565266L },
            { 0xd9e3bf7b, 11568978L },
            { 0xda2b8f79, 4611614L },
            { 0xda3bf619, 6993761L },
            { 0xda6132ff, 6827154L },
            { 0xda746262, 2432986L },
            { 0xda87b38c, 9920608L },
            { 0xda952297, 6691932L },
            { 0xdab48923, 6602358L },
            { 0xdac39ea9, 8614667L },
            { 0xdb240a0e, 6586686L },
            { 0xdb70dbc1, 10011492L },
            { 0xdbeb6614, 4527705L },
            { 0xdc046a4c, 5517893L },
            { 0xdc0b2d48, 9089996L },
            { 0xdc0bef65, 8819509L },
            { 0xdc2dd727, 8316496L },
            { 0xdcb5a769, 5376703L },
            { 0xdce934ea, 7709119L },
            { 0xdd2cd9be, 7780999L },
            { 0xdd4e8362, 7309204L },
            { 0xddae91b4, 6237315L },
            { 0xddbb7f1d, 6445010L },
            { 0xddecd497, 9785089L },
            { 0xddf205e3, 9937468L },
            { 0xde802250, 10017129L },
            { 0xdec0fdda, 5884890L },
            { 0xdf77ee12, 10975238L },
            { 0xdf9adf21, 8662283L },
            { 0xdfbb8259, 5028356L },
            { 0xdfd8f77a, 2040130L },
            { 0xdfde21e4, 8359722L },
            { 0xe075d96b, 6405045L },
            { 0xe0df7c0d, 9269337L },
            { 0xe0e2da61, 7780857L },
            { 0xe0f3926b, 9737267L },
            { 0xe0f69b8f, 10323207L },
            { 0xe112d26c, 11605477L },
            { 0xe14bfa66, 7378329L },
            { 0xe1577be5, 8470140L },
            { 0xe163b26c, 8247513L },
            { 0xe19c2cff, 8929704L },
            { 0xe1b42acd, 9518653L },
            { 0xe209959f, 6116812L },
            { 0xe23f9020, 2830132L },
            { 0xe4440b9b, 7233365L },
            { 0xe445b4a6, 8060924L },
            { 0xe482787c, 3989047L },
            { 0xe48328e1, 10795945L },
            { 0xe48c2a23, 7772545L },
            { 0xe48d0746, 6442091L },
            { 0xe4a3dca2, 8122988L },
            { 0xe4fb60eb, 11287908L },
            { 0xe5293e72, 4603103L },
            { 0xe53b581a, 4330559L },
            { 0xe53fa2df, 11391626L },
            { 0xe542c148, 11315112L },
            { 0xe56eb692, 8666626L },
            { 0xe5c378c5, 12881119L },
            { 0xe5d2ff1f, 9935171L },
            { 0xe60e754c, 6065894L },
            { 0xe636aa91, 7641050L },
            { 0xe6888167, 8742981L },
            { 0xe69e35c0, 5296157L },
            { 0xe6e7f18b, 7762460L },
            { 0xe6fb10f3, 6390428L },
            { 0xe735b451, 9005468L },
            { 0xe7938987, 13285309L },
            { 0xe7d374d8, 5549101L },
            { 0xe7d90c21, 6864605L },
            { 0xe82917b6, 12275805L },
            { 0xe89c0e48, 7063854L },
            { 0xe89ff41c, 9163895L },
            { 0xe8db9140, 5399168L },
            { 0xe8e88d38, 4777648L },
            { 0xe93c8663, 6825265L },
            { 0xe94250e9, 4546575L },
            { 0xe9693f98, 10106650L },
            { 0xe999932c, 6005035L },
            { 0xe9a90c22, 6148278L },
            { 0xe9f11574, 9692542L },
            { 0xe9fc1f39, 6891803L },
            { 0xea2f9b26, 10849723L },
            { 0xea50c82f, 5377585L },
            { 0xea71c9dc, 4779373L },
            { 0xeac3d84f, 12342827L },
            { 0xeb686709, 10994185L },
            { 0xeb6c0a92, 6989241L },
            { 0xeb945bc1, 7711463L },
            { 0xebbd5efc, 5045553L },
            { 0xebdc8225, 4084881L },
            { 0xebe254b3, 11202008L },
            { 0xec0f97f3, 7596795L },
            { 0xec3b09c0, 5213755L },
            { 0xec86699b, 3485455L },
            { 0xeca8d234, 8680697L },
            { 0xecadc9fd, 6333960L },
            { 0xecf7dde6, 7964522L },
            { 0xed8bea88, 7586239L },
            { 0xede77ffb, 10900529L },
            { 0xee3f527d, 3939683L },
            { 0xee72ed45, 7634142L },
            { 0xee8dc760, 7151027L },
            { 0xeea7d425, 2028555L },
            { 0xeea95801, 6292998L },
            { 0xeebceb18, 6456404L },
            { 0xeec5748a, 7252122L },
            { 0xeeeb7597, 5810147L },
            { 0xefb6a70c, 8538425L },
            { 0xefba77ca, 4375808L },
            { 0xefbdcfbf, 5588713L },
            { 0xefc4c201, 8568597L },
            { 0xeff1fdb0, 5246142L },
            { 0xf0ce033e, 6380425L },
            { 0xf0d9ae7f, 8872016L },
            { 0xf126ef7c, 6932782L },
            { 0xf14f637a, 9144056L },
            { 0xf157fd2a, 8723928L },
            { 0xf15b8010, 7312349L },
            { 0xf191c159, 13652765L },
            { 0xf1f37c98, 3291963L },
            { 0xf2010049, 10701696L },
            { 0xf219d380, 8939529L },
            { 0xf2999f51, 11542428L },
            { 0xf2e97a59, 7494394L },
            { 0xf3b82ddf, 7266306L },
            { 0xf3ca84b4, 8341235L },
            { 0xf3ccccbe, 6469922L },
            { 0xf3f43ce9, 11846213L },
            { 0xf3fab854, 11351221L },
            { 0xf4126cc4, 3528974L },
            { 0xf416b3ec, 6487687L },
            { 0xf4e5edc9, 6968698L },
            { 0xf538d3c7, 9061345L },
            { 0xf53eaf26, 10494400L },
            { 0xf56ce72c, 9427118L },
            { 0xf5b89805, 10467990L },
            { 0xf60db645, 10487244L },
            { 0xf6159793, 15636227L },
            { 0xf61d92e8, 9643047L },
            { 0xf662fbb4, 7531301L },
            { 0xf718d902, 12237497L },
            { 0xf73f0765, 6887699L },
            { 0xf784f104, 6428349L },
            { 0xf78e916d, 7521556L },
            { 0xf78ed967, 11651823L },
            { 0xf78f37e6, 10180112L },
            { 0xf79a89c2, 8404271L },
            { 0xf7d7940c, 10582119L },
            { 0xf7f1f13c, 9883475L },
            { 0xf86f37e4, 5642085L },
            { 0xf88e6aac, 5295862L },
            { 0xf8c07e5d, 7690549L },
            { 0xf8e3fc32, 7111371L },
            { 0xf8f818e1, 7373075L },
            { 0xf9ca6b20, 6925813L },
            { 0xf9e11d1c, 6559953L },
            { 0xfa733902, 11660478L },
            { 0xfac41fac, 7839819L },
            { 0xfaf6c024, 7733829L },
            { 0xfbc9619c, 10045063L },
            { 0xfc55fe0a, 5619157L },
            { 0xfc650ed2, 7218061L },
            { 0xfc697613, 8048004L },
            { 0xfc8d548b, 6498063L },
            { 0xfca517aa, 6636211L },
            { 0xfd03db16, 15011875L },
            { 0xfd746ad2, 6245990L },
            { 0xfd938797, 6838858L },
            { 0xfddca51c, 7956930L },
            { 0xfdef7fc2, 9280717L },
            { 0xfe1a5de6, 9174353L },
            { 0xfe7b8edb, 8739206L },
            { 0xfe7c3fec, 6843626L },
            { 0xfea08f34, 10781442L },
            { 0xfeac70dc, 3682878L },
            { 0xfee30120, 5546239L },
            { 0xfefda8d6, 5245333L },
            { 0xff7d2ca4, 11139002L },
            { 0xff97ba8f, 11352834L },
            { 0xff9f18e0, 9969784L },
            { 0xffbb3b82, 4787701L },
            { 0xffbe1d84, 6852765L },
            { 0xffc0ea58, 12389277L },
        };

        // Round 53 IFF-LITERAL buy-catalog ENUMERATION-ORDER canon: generated by
        // tools/iff_objd_catalog_order.py over game-data/The Sims (1114 GUIDs). Served catalog order
        // must equal: category-major asc (Category = IFF OBJD log2(FunctionFlags) or BuildModeType+7),
        // then ORIGINAL IFF FAR-member encounter order, cross-file IFF path-sorted.
                public static readonly uint[] CatalogOrderArray = new uint[] {
            0xdcb5a769,
            0xfdef7fc2,
            0xabfc4be9,
            0xabfc34d3,
            0x7c21272e,
            0x2e9fdca5,
            0x6bf6d402,
            0x431d46c8,
            0x2be7cbba,
            0xd4588282,
            0x3c71d2e9,
            0x663eaffc,
            0x17579980,
            0x9985a06f,
            0x996de865,
            0x994fb8f8,
            0x41fdd8d8,
            0x795e70b6,
            0x502367de,
            0xb6236b64,
            0xe82917b6,
            0xe9f11574,
            0x1584c383,
            0xc48f31a0,
            0xddbb7f1d,
            0xb99d5c6e,
            0xc58b5c78,
            0x328ed9e1,
            0xa9ec8419,
            0x254233ad,
            0xfeac70dc,
            0x823a9ee0,
            0xed8bea88,
            0xdc046a4c,
            0xeec5748a,
            0xd2944a33,
            0x6585d104,
            0x0f8beda7,
            0x55bcbc04,
            0xcbe53119,
            0x24dad5c4,
            0x0924bc1f,
            0x7280cbfc,
            0xcaf38382,
            0x738a419a,
            0xd73d1f82,
            0xd4bf28f3,
            0x2768de7d,
            0x3c30cb5d,
            0x81c7a5e0,
            0x12d9e6a3,
            0xc2f1a050,
            0x2796b726,
            0x00c2b68f,
            0xfe7b8edb,
            0x6cc079c2,
            0xae1daf72,
            0x0768a9cb,
            0xa4d8c299,
            0xbc4b1c4f,
            0xbc4b7a23,
            0xc0e64804,
            0x8c9350d2,
            0xf5b89805,
            0xfc55fe0a,
            0xa08e0c14,
            0x3e69a01d,
            0x42316f67,
            0xe0df7c0d,
            0x228e96d2,
            0x371e3b21,
            0x7c5e5c6d,
            0xf86f37e4,
            0x38232e50,
            0x32f5a529,
            0x66548be0,
            0xf4e5edc9,
            0xf15b8010,
            0x9b3cdd30,
            0x66237c5d,
            0x6da8b998,
            0x645365da,
            0xbcab486a,
            0x87c44d57,
            0xa1d6fe56,
            0x8f01bdcb,
            0x34f7be98,
            0xbf51b5f3,
            0xe6888167,
            0xf784f104,
            0x2fcd8a30,
            0xa94c3259,
            0xd1180b2a,
            0x59c389bc,
            0xe7938987,
            0x6a3889f8,
            0xd97681db,
            0x26bfbb29,
            0x25b82d98,
            0xd8bb3aa8,
            0xfaf6c024,
            0x7e97444a,
            0x9be0e2bf,
            0x4ef4ea16,
            0x9082401f,
            0x0fbb8bf8,
            0x87d00adc,
            0xeeeb7597,
            0x5ede0bc4,
            0x9f12673e,
            0x334f732f,
            0xad4d0d62,
            0x7cb11019,
            0xe94250e9,
            0x81179ab3,
            0x82e04c5b,
            0x3089d8d2,
            0x0e82c943,
            0x6593d291,
            0x82e0f146,
            0xf79a89c2,
            0x22920f55,
            0x99120547,
            0xf3b82ddf,
            0xd9ae1ae5,
            0xd95f52ef,
            0xcda8d70a,
            0xab101956,
            0x3480731c,
            0xaaf447d6,
            0x7f283620,
            0x294439e2,
            0x391e258e,
            0xe163b26c,
            0xe14bfa66,
            0x1ad5b840,
            0x0125da9d,
            0x01259297,
            0x048567b7,
            0x144a7815,
            0x3658d1aa,
            0x2ab64a1d,
            0x1bbdb732,
            0x9f3a38b9,
            0xd84d39ce,
            0xffbb3b82,
            0x4a21d3a7,
            0xc8ebdd42,
            0x68c11912,
            0x0d192399,
            0xe0e2da61,
            0xe0f3926b,
            0xa1755ff7,
            0x801f7235,
            0xe5c378c5,
            0xa46aed33,
            0xe93c8663,
            0x099a621e,
            0x4a8ae8fb,
            0x0aec8c97,
            0x77d9944d,
            0xfc650ed2,
            0x96a5928b,
            0xd3a28b82,
            0xcf000516,
            0xf61d92e8,
            0x3742732c,
            0x9c59140e,
            0x88417fac,
            0xba7e8047,
            0x60e23da6,
            0xe999932c,
            0x7b6c8c63,
            0xcf62b688,
            0xd5a34f0f,
            0x6106a1a7,
            0x4c442733,
            0x4c446f39,
            0x4c443fa4,
            0x36113a6d,
            0x1cc44f98,
            0xa072951f,
            0x25ba0090,
            0xefbdcfbf,
            0xdb70dbc1,
            0x74d93e95,
            0xbb1bcf68,
            0x6ad59dd3,
            0xcb6e77ab,
            0xae12c97a,
            0xae5c99e7,
            0xa5b9515a,
            0xf3f43ce9,
            0x151d17ed,
            0x0a87e8cf,
            0x0a02f059,
            0x1d8e6e89,
            0x91767a36,
            0x47e917c4,
            0x73d6180d,
            0x8bd70015,
            0x1dff3cab,
            0xd9a73952,
            0xeb6c0a92,
            0xa90a7d01,
            0x5a5cda09,
            0x921fda07,
            0xc1b6da6a,
            0x0133d442,
            0x8c9e33c9,
            0x7939d97d,
            0xcc944729,
            0x99e0a9b1,
            0x2bd867fe,
            0x3e6fda2f,
            0x02f04067,
            0xf1f37c98,
            0xfea08f34,
            0xcdd887b1,
            0xe542c148,
            0x882480a6,
            0x8e3520bd,
            0x068ead71,
            0x7d4a0f76,
            0x404ee381,
            0x0bfdfb2a,
            0x2d0a3de1,
            0x4699596d,
            0xf219d380,
            0xdfbb8259,
            0x45caf478,
            0x80858fd1,
            0x56276590,
            0xebbd5efc,
            0x9f733d4f,
            0xfefda8d6,
            0x2644d3c7,
            0x87c99127,
            0xe0f69b8f,
            0x3672a8b2,
            0xcbef938d,
            0xcbeff420,
            0x68897ff8,
            0xaf85a57c,
            0x849207af,
            0xb1230866,
            0xaed36fcb,
            0xe69e35c0,
            0x30bcda26,
            0x7387f3a2,
            0xf2010049,
            0x629be21f,
            0x85f1a085,
            0xeebceb18,
            0xa01e500d,
            0x9fa148dc,
            0xeb686709,
            0xff9f18e0,
            0xbb626ef3,
            0xad08fdc0,
            0x1fac199d,
            0x675c18af,
            0x42c078b3,
            0xb94755df,
            0x253c1a5e,
            0xbf137195,
            0x29cb5a51,
            0x91ed6984,
            0x7b21053a,
            0x7e853dea,
            0x1e8b9ea7,
            0xe8e88d38,
            0x6dd88e6d,
            0x9cb1bb9b,
            0x5e8b157a,
            0x0afae8b0,
            0x8da9ffb4,
            0xea2f9b26,
            0x74e7ffa9,
            0x536a3e4d,
            0xe4440b9b,
            0x54c7f70d,
            0xdd4e8362,
            0x6954e030,
            0x2fdf80dd,
            0x6923633a,
            0x8613a26a,
            0x0e4f2e14,
            0x594a89f0,
            0x85e4adbe,
            0xe209959f,
            0x81d97262,
            0x3b0971f3,
            0xad4cf878,
            0x1b833d24,
            0x8ad987b8,
            0x8e8d9ca9,
            0x6a776e73,
            0xede77ffb,
            0xd191aff7,
            0xd191ff6a,
            0x835ba084,
            0xafabcc3d,
            0xeea7d425,
            0x481a74ec,
            0x46f4e80b,
            0xd49346ff,
            0xaa2f9672,
            0xb867117e,
            0xb499ec29,
            0xfc8d548b,
            0xc192dacc,
            0x5fa381c1,
            0xd2a03993,
            0x86e0bbb5,
            0x6b264238,
            0xa483d725,
            0xab647747,
            0xf416b3ec,
            0x3794bcc5,
            0x96c58db2,
            0x852df95d,
            0x582f5936,
            0x6e0ec37e,
            0xb7cd406c,
            0xeac3d84f,
            0xd1bc4074,
            0x4ee8b3cf,
            0xebe254b3,
            0x83023da6,
            0xe19c2cff,
            0xea71c9dc,
            0x6fb00726,
            0x0a8178e9,
            0x00ddfef9,
            0x58dde126,
            0x5bfb6140,
            0x46ddd450,
            0x2c7dff0e,
            0x4c103662,
            0x2c57d9f1,
            0x1772dd4f,
            0xeea95801,
            0x52ae7a4e,
            0x5b182c35,
            0x6c582f42,
            0xa7340ec3,
            0x7ed5d1b6,
            0x8fed54c2,
            0x85e02a40,
            0x84e0774c,
            0x3c566968,
            0x85e00942,
            0xdbeb6614,
            0xe445b4a6,
            0x34fe4e17,
            0x75ac188c,
            0x4c727c5d,
            0xebdc8225,
            0xf7d7940c,
            0x7839c094,
            0x93f98c8a,
            0x59132604,
            0x59133e93,
            0x135e770f,
            0x6c86123e,
            0xad591aac,
            0xf9ca6b20,
            0x62b6f3c1,
            0xbba46c20,
            0x2fc581ad,
            0x2f92cd8f,
            0xbc661500,
            0x15316867,
            0x15445522,
            0x6a55e034,
            0xa1d5048a,
            0xa1bd4c80,
            0xa1594e42,
            0xa13f39cf,
            0xa11d2875,
            0xa2666952,
            0x5880d445,
            0x5d0bbdb6,
            0x8d70f7b3,
            0xdc2dd727,
            0x1f15af7e,
            0x855cde99,
            0x342a5e00,
            0xb48a1144,
            0x4ae4f77c,
            0x95efe015,
            0x877feca1,
            0xdac39ea9,
            0x375dd48b,
            0xa805e814,
            0xa478d407,
            0xeff1fdb0,
            0x029c9043,
            0x7ab03de4,
            0xc26265b3,
            0x1de172c2,
            0x79fa7e6b,
            0x601b0c9a,
            0x89173b24,
            0x34acf253,
            0x9293b59e,
            0x5edb929d,
            0x94fd43dc,
            0x2e7f37ea,
            0x417e691c,
            0x5fb93288,
            0x37bfa925,
            0x1e6d621b,
            0xb4b333e4,
            0xe7d374d8,
            0x01f02564,
            0xec3b09c0,
            0xc73c14a4,
            0xc4c35536,
            0xd608b270,
            0x952bcac7,
            0x969bde47,
            0xc7a7215c,
            0x5359bfc1,
            0x3c52b66d,
            0x8eb8e1a6,
            0xf3ccccbe,
            0xf3ca84b4,
            0x0ce4a17c,
            0x1e8dd05c,
            0xefba77ca,
            0xbc12cd61,
            0xad67a694,
            0xad67ee9e,
            0xad67be03,
            0xb79f3182,
            0x2263dceb,
            0x5cb56b2d,
            0x16aed8d8,
            0x191b9479,
            0xc7d177e1,
            0xe5d2ff1f,
            0x8b04d5e1,
            0x43b62717,
            0xb89b2451,
            0x8a712a85,
            0xe8db9140,
            0x36f33011,
            0x718ea9c7,
            0x57f68405,
            0xb78e7f8d,
            0x12a45904,
            0xc3efd759,
            0x9367e97a,
            0x39ce4936,
            0xb0706e2a,
            0xb01c2620,
            0xb1b176bd,
            0x7f1ae4cf,
            0x4371fde9,
            0x4c659e7d,
            0x0ffc192a,
            0x6bad868b,
            0xad2c5018,
            0xb3760ba8,
            0xc71e43cd,
            0x6928408e,
            0x43a116b4,
            0x43a15ebe,
            0x724ca405,
            0xdce934ea,
            0xb407a783,
            0x52c81f9b,
            0x1181ccb1,
            0xdec0fdda,
            0x65396e67,
            0x3bc18581,
            0xbe1d7003,
            0xbb0846ea,
            0x9fd45e39,
            0x171527eb,
            0x17f06fe1,
            0x144c3f7c,
            0x14284046,
            0xb797465f,
            0xb46b0e55,
            0xb5250c97,
            0xfe1a5de6,
            0x1c6b6d6a,
            0x6e325f22,
            0x70760da9,
            0x471a0bf6,
            0xb31edff8,
            0x81a87bab,
            0x984671a9,
            0x398cc7a0,
            0x48a153ad,
            0xec86699b,
            0x88103f3e,
            0x39ed3b5a,
            0x39dd7192,
            0x384c487b,
            0xa0ee756f,
            0x8d4ee7df,
            0x50b9bc6d,
            0xfac41fac,
            0xc0e20936,
            0x702851b4,
            0x6c9f7100,
            0xacee4ba5,
            0xb159c37d,
            0x90af09f4,
            0xca55ed92,
            0xefc4c201,
            0xbd78c75d,
            0x8fb1bf00,
            0x4af2067e,
            0x49402e4f,
            0x2a7d9829,
            0xca528b1d,
            0xcbc6c317,
            0xcf89e47b,
            0xddae91b4,
            0xdd2cd9be,
            0xdab48923,
            0xda3bf619,
            0x0c189d36,
            0x0b36e878,
            0x95624034,
            0xae6e43d6,
            0xdc0b2d48,
            0xfbc9619c,
            0x64f0e2bc,
            0xb796426e,
            0x157021de,
            0x67406e73,
            0xa0af4e74,
            0x28a0403e,
            0x15597b59,
            0x37326795,
            0x70fc19c3,
            0xfee30120,
            0x8f033944,
            0x8dab571f,
            0x1b490662,
            0x5250b4c1,
            0xbc3ba088,
            0xd9e3bf7b,
            0x2ffaa585,
            0xabe586cd,
            0x7d2089b6,
            0xad12e0d0,
            0x9ae018bb,
            0xaf4a1300,
            0xe5293e72,
            0x78edcd25,
            0xff97ba8f,
            0xb346903c,
            0x2a7994bf,
            0x52112235,
            0x4b69ea5a,
            0xa82ca08e,
            0x3659dca2,
            0x98e0f8bd,
            0xe60e754c,
            0x813a5a6b,
            0xce47dd58,
            0xc96b93eb,
            0xd1ef8174,
            0xc606154b,
            0xd389c839,
            0x515e8635,
            0x72a5cbe3,
            0xa6caf1dd,
            0xa607cc98,
            0x997beef4,
            0xb62454df,
            0x42197abc,
            0x98e9cfe2,
            0x3dcc1abe,
            0x6c1bb706,
            0xe1b42acd,
            0x21f954a6,
            0x8f300ef1,
            0x405046ff,
            0x21e65bbf,
            0x49f9c16f,
            0xf662fbb4,
            0x5faa302a,
            0xda2b8f79,
            0xa91403ab,
            0xa91d4963,
            0xf718d902,
            0xd2bb9953,
            0x35eaebdc,
            0x84ceb10b,
            0x5a1bc4bc,
            0x47a99575,
            0xa8bdd25b,
            0xf60db645,
            0x0b1ad6cc,
            0xda952297,
            0xbe773f58,
            0xad0b9dd6,
            0x90c1c7cb,
            0xf14f637a,
            0x65cb152b,
            0x54b3b81a,
            0xeb945bc1,
            0xc74e7da7,
            0x2f86506b,
            0x2d5f0d6c,
            0x4deefc68,
            0x26e91ef5,
            0x26ad56ff,
            0x26ad0662,
            0x78d7ea4d,
            0x0ae5df54,
            0x5a01eb5b,
            0x4008dbb7,
            0x5e9e42fc,
            0xee8dc760,
            0xc4370e53,
            0x1ab85e45,
            0xc349c14c,
            0x66131ba8,
            0x7051ee96,
            0x52f83bea,
            0x5249b60e,
            0xa7a70f83,
            0x2e10faec,
            0x7bfdcc88,
            0x243f0fa0,
            0x60d99178,
            0x14ef6729,
            0xbfc58dd0,
            0x5156efbc,
            0x85b424a2,
            0x85b46ca8,
            0x0ecbec1c,
            0x0f1aa416,
            0x97a845c3,
            0x722a0fbf,
            0x8841dbb9,
            0x4ef80f89,
            0x1bb40a4e,
            0x7a8fdbf4,
            0xf78ed967,
            0xf78e916d,
            0xf3fab854,
            0xec0f97f3,
            0xeca8d234,
            0x246f6a23,
            0xdb240a0e,
            0x00fe9850,
            0x6a53de3c,
            0x9b3340b9,
            0x9aa2312f,
            0x1694348f,
            0xd985a7ad,
            0x9f7295a9,
            0xffc0ea58,
            0x1f287b65,
            0x3d3e7889,
            0x6350e96f,
            0x70b0d5f2,
            0xbc537c52,
            0x96cb67ae,
            0x8ff0ca39,
            0x4188ca2b,
            0x7318c9df,
            0x90c7c9e4,
            0x4fdec997,
            0x05dac986,
            0xa6a4a1ac,
            0x04d86d1f,
            0x5dac929f,
            0xf8e3fc32,
            0x68af92a3,
            0xf126ef7c,
            0x182e7c24,
            0x2ecdb068,
            0x2b4502a2,
            0xe23f9020,
            0x5152a864,
            0x64983e4d,
            0xb56a0aff,
            0xbedd7b26,
            0xac262843,
            0x8eec19f7,
            0x165cd872,
            0xb26ab256,
            0x81b3a551,
            0x7a56922f,
            0x8592f25b,
            0xa77f36ae,
            0x3ac7f3be,
            0x05777d82,
            0x1e743c39,
            0xd7404661,
            0xe112d26c,
            0x7bea0977,
            0x4a70df92,
            0xc523d73d,
            0x6a8594ad,
            0xa5004e8c,
            0x52f6acd7,
            0x4cd0523d,
            0x1d772052,
            0xaae98936,
            0x5a91cee0,
            0xcc66ad61,
            0xa9c05832,
            0x645542df,
            0x4450e4e0,
            0xf4126cc4,
            0xe89c0e48,
            0x88734ce8,
            0x70467b85,
            0xd45e4bea,
            0x7f775eee,
            0x547f4934,
            0x66877bef,
            0xad005811,
            0x160bb3d1,
            0x17fd98f7,
            0xda87b38c,
            0x4d6da494,
            0x2077e7de,
            0x000c147f,
            0x2a3d8714,
            0x26d24804,
            0x2521000e,
            0x95d4de33,
            0x95b09639,
            0x637692ea,
            0x880bb75e,
            0x2cfb155a,
            0xa489640d,
            0xecadc9fd,
            0xfc697613,
            0x41e1f6f3,
            0x4be4809a,
            0xcfd10860,
            0x552a8297,
            0x62ccb570,
            0x35b26d7f,
            0x2b956ab9,
            0xe9a90c22,
            0xffbe1d84,
            0x2baa0029,
            0xa5c0bb1e,
            0xdfde21e4,
            0xc7efe216,
            0xcce4be4f,
            0x31edeb80,
            0xf157fd2a,
            0xb80950cc,
            0x94082f6d,
            0xcf5a1a2f,
            0x3aa14a49,
            0x30f4b933,
            0xf73f0765,
            0x72fa6739,
            0xddf205e3,
            0x4596ee35,
            0x765bcec0,
            0x6aeb3d99,
            0x7ef99b7e,
            0x40c778fd,
            0x6188f5d7,
            0xd0a0f269,
            0x2130c4e5,
            0x578f5050,
            0x8ec5750d,
            0xce3615a5,
            0x6691c3d8,
            0xce4b38cf,
            0xf0d9ae7f,
            0x4ddf498c,
            0x00230edc,
            0x9fb223ce,
            0xddecd497,
            0x9d4b03a5,
            0xdfd8f77a,
            0xc041ed5b,
            0x112039a0,
            0x7e1f25b1,
            0x47f39f51,
            0x11a92d74,
            0x83e0af58,
            0xfddca51c,
            0x5cdc712f,
            0x9b3a5cdf,
            0xba9dc16a,
            0x94aa32f4,
            0x7726e3a6,
            0x0fd4ccef,
            0xe075d96b,
            0x294a48ee,
            0xc2a91fd0,
            0xa022c70c,
            0xee3f527d,
            0xe53fa2df,
            0x39262c4c,
            0x4f8e1c8b,
            0x108262be,
            0x7fdf0fc1,
            0x7fd422a4,
            0x7fbe5529,
            0x7fab4493,
            0xfd03db16,
            0xf6159793,
            0x2f42fc26,
            0xa6aebf5b,
            0x94f5cda8,
            0xfd938797,
            0xefb6a70c,
            0xdf9adf21,
            0xcd39bb3a,
            0x0dfd8714,
            0x6c7caec2,
            0xda746262,
            0xda6132ff,
            0x00e22e1e,
            0xc25fcf86,
            0x4a459a5d,
            0xb87a8d3f,
            0x3dbfc043,
            0x4126d70f,
            0x4e1ea389,
            0x8f667b23,
            0x7e09517e,
            0x59b200b5,
            0x77d429bb,
            0x29eaef1f,
            0x46ef5577,
            0x7d2995e4,
            0x6610c43c,
            0xcbd2be4e,
            0xf191c159,
            0x0f77759b,
            0x5d071f26,
            0xba71a336,
            0x7e56b1be,
            0xa08dd989,
            0x2d773bf9,
            0x92c87dc0,
            0xa22c5865,
            0x46e807b4,
            0x2ba019c7,
            0xea50c82f,
            0x4e665bf5,
            0xe53b581a,
            0x0c0d4574,
            0xb21b9510,
            0xe735b451,
            0x3d1f6466,
            0x9c223167,
            0x94d2df9d,
            0xf78f37e6,
            0xc7d6cf27,
            0x6c3bbc3e,
            0x6cb7f434,
            0x5b048a52,
            0xe1577be5,
            0x98a0a6d6,
            0x19057c09,
            0xa4e75cff,
            0x41c753c1,
            0xff7d2ca4,
            0xcb386e73,
            0xc884fc9e,
            0x98e09eb3,
            0x2c501364,
            0xfe7c3fec,
            0xdf77ee12,
            0x2c5ac343,
            0xfd746ad2,
            0x28975923,
            0xba67ead2,
            0x0d94642f,
            0xb668dd56,
            0xb7eaacba,
            0xb790a819,
            0x16d3a20d,
            0x2935d383,
            0xd4709f75,
            0x88ede461,
            0x6f35df40,
            0x6f2bb8ed,
            0x6c8bd4a3,
            0x0d2dde73,
            0x56007c4b,
            0x21332b58,
            0x4a4d1966,
            0xbde052c5,
            0x17fc774d,
            0xe6e7f18b,
            0xe636aa91,
            0xcf5b61d3,
            0xcf18067e,
            0xcfe94d2c,
            0xce6c2041,
            0xce6c4713,
            0xce4d6011,
            0x9ee7eb60,
            0x60909779,
            0x8cb4d449,
            0xecf7dde6,
            0xf8c07e5d,
            0x7cab54a2,
            0x8fca5a7b,
            0x62f8a7e4,
            0xe56eb692,
            0x55acb9e0,
            0xc4387388,
            0x63a3d7d4,
            0xce10c801,
            0xc56562c1,
            0xf2e97a59,
            0x66025e42,
            0x8e880484,
            0xc69b6e73,
            0x16f14e74,
            0x000001ca,
            0x9a0d8249,
            0x2aca7211,
            0xc3927fa3,
            0x23941850,
            0x4fa133c9,
            0x0c04ca02,
            0x0d4ce6fd,
            0x0d14b885,
            0x0ede95bb,
            0xd158b046,
            0x7f0abd26,
            0x8c44ba58,
            0x912cbed4,
            0x862ccc31,
            0x79e2bb28,
            0x2a262d32,
            0xb53903aa,
            0xd0a244e4,
            0xd0b754e0,
            0xcb790e58,
            0xf88e6aac,
            0x15717e80,
            0xee72ed45,
            0xe7d90c21,
            0x14ae1e58,
            0x2f8715d1,
            0x41a50757,
            0xf7f1f13c,
            0x51a767e2,
            0x6065ac2d,
            0x615c9169,
            0xc6448ae4,
            0x00e30dc2,
            0x7d9b1a47,
            0xbcc81aff,
            0xba6e27bb,
            0x95a59fd2,
            0x8bf2e852,
            0xbeb0f165,
            0x5d39291c,
            0x5b561459,
            0x09196e73,
            0x64a10860,
            0xc5d8a768,
            0x5199e127,
            0x2256ba70,
            0xd735a326,
            0xf538d3c7,
            0xa67ac823,
            0x44e8992a,
            0x8942bdc7,
            0x37186734,
            0xe9fc1f39,
            0x5295a7a8,
            0x9667a489,
            0xbf1b918e,
            0x69bd37af,
            0x00c8a5f1,
            0x031db391,
            0xf8f818e1,
            0x948aed3c,
            0x3288ed87,
            0x88b6a098,
            0x73eef299,
            0xa2fe803a,
            0xbdcf6469,
            0x3c9ea694,
            0x6d96dcfa,
            0x307ddfa6,
            0x5d767e1f,
            0x5d723615,
            0x5d7b6688,
            0x15460fca,
            0x154522af,
            0xe4fb60eb,
            0xe48328e1,
            0xe482787c,
            0xe48d0746,
            0xe48c2a23,
            0x0eb692fb,
            0xac66d6ca,
            0x1671a218,
            0x00dd7908,
            0x8ba56284,
            0x51474702,
            0x5f421524,
            0xc99ac74e,
            0x440087f3,
            0xd9bd21cd,
            0x3484c1f7,
            0x9da7b210,
            0xde802250,
            0x11ec66d9,
            0x52ed4b7c,
            0xb1153e7d,
            0x72bdbb7d,
            0xaed3c88a,
            0xdc0bef65,
            0x34431396,
            0x3026a0f4,
            0xaa86ad2a,
            0x3007996e,
            0x17332210,
            0x54c3e0cf,
            0x6854da61,
            0xceb2f9b8,
            0xf2999f51,
            0x678f09ff,
            0x7c24a376,
            0xe9693f98,
            0x110cf4b1,
            0xf9e11d1c,
            0xb9020120,
            0xfa733902,
            0x10444309,
            0x2abc6e73,
            0x99400b4a,
            0xa02c8857,
            0xbb83a08a,
            0xd68e73d8,
            0x2c555ea3,
            0xd5ca5425,
            0x4b4d30a9,
            0x1468c3a7,
            0x09d8dcec,
            0x2b83b971,
            0x39319021,
            0x5c198cbe,
            0x96e3398d,
            0xa2daa08c,
            0x65274a4f,
            0xc02b406a,
            0x3def46a0,
            0x3dea8b15,
            0x5d6b538a,
            0xb5215809,
            0x8eaa68ad,
            0x497a0c3e,
            0x0fe5fa64,
            0x2b49a3f4,
            0x61b7982e,
            0x4bd0b785,
            0xf0ce033e,
            0xe89ff41c,
            0xb9b0c862,
            0xa8ad8714,
            0xa8c59f83,
            0x6f93a84a,
            0x4b78bf0e,
            0x75b0b2b7,
            0x5a6feced,
            0x5a12bc70,
            0x4f51d969,
            0x36fe2161,
            0x354a1785,
            0xc6b2bdd5,
            0x09cc9d1c,
            0x51c23b85,
            0x2f014f68,
            0x473532d6,
            0x3313d86b,
            0x9a37fba7,
            0xfca517aa,
            0x83eb896c,
            0x359e63bd,
            0x22dd513c,
            0x0a717442,
            0xe6fb10f3,
            0x27f8f51f,
            0x42bcc380,
            0x2b89fd8a,
            0x2bb9b580,
            0x2bb9e51d,
            0x05d17306,
            0x5b088f5b,
            0xf53eaf26,
            0xf56ce72c,
            0xe4a3dca2,
            0x8d1b6101,
            0xaa655e9f,
            0x9c541464,
            0x2e453ea5,
            0x9ede28ae,
            0xaa75a097,
            0x77d26881,
            0x31a0e42c,
            0x4208b8e3,
        };

        // Round 54 IFF-LITERAL sound-corpus census: generated by tools/iff_objd_audio_census.py over
        // game-data/The Sims - DISTINCT original IFF member filenames per engine sound extension
        // (.wav .mp3 .xa .utk). The engine TS1Audio must mount exactly this corpus (no invention/loss).
                public static readonly Dictionary<string, int> SoundMountCensus = new Dictionary<string, int>{
            { ".wav", 284 },
            { ".mp3", 0 },
            { ".xa", 17438 },
            { ".utk", 0 },
        };        public static readonly Dictionary<string, int> ResourceMountCensus = new Dictionary<string, int>{
            { "", 26 },
            { ".bat", 1 },
            { ".bcf", 5165 },
            { ".bmf", 3156 },
            { ".bmp", 4911 },
            { ".cfg", 1 },
            { ".cfp", 7977 },
            { ".cmx", 151 },
            { ".cur", 51 },
            { ".ffn", 45 },
            { ".flr", 198 },
            { ".fon", 7 },
            { ".fsc", 4 },
            { ".h", 5 },
            { ".hdb", 3 },
            { ".hit", 25 },
            { ".hot", 25 },
            { ".hsm", 25 },
            { ".iff", 1203 },
            { ".max", 1 },
            { ".ndx", 68 },
            { ".omk", 1 },
            { ".pal", 1 },
            { ".rt", 4 },
            { ".sfk", 1 },
            { ".skn", 2998 },
            { ".spf", 24 },
            { ".stx", 856 },
            { ".syslog", 1 },
            { ".tga", 16 },
            { ".txt", 24 },
            { ".wav", 286 },
            { ".wll", 259 },
            { ".xa", 19060 },
            { ".xml", 12 },
        };

        // Round 56 IFF-LITERAL OBJD-corpus census (tools/iff_objd_census.py over game-data/The Sims,
        // IFF data: 5125 IFF OBJD chunks (5123 v138 + 2 v136), 5120 DISTINCT GUIDs - 5 IFF duplicate-GUID
        // chunks; the engine OBJ mount is GUID-keyed like the ORIGINAL game, so IFF-literalism is the
        // distinct-GUID corpus).
        public const int ObjdCorpusCensus = 5120;

        // Round 57 IFF-LITERAL STR-content census (tools/iff_ctss_census.py over game-data/The Sims,
        // IFF data: 1895 CTSS chunks / 3769 catalog-text STR blocks per member BASENAME last-wins
        // (mirror engine mount: duplicate-basename members across packs are IFF-literally shadowed),
        // engine STR.Read mirror: fmt 0/-1/-2 all blocks; fmt -3 only language 0/1 items).
        public const int CtssBlockCensus = 3769;
        public const int CtssChunkCensus = 1895;

        // Round 58 IFF-LITERAL STR-family census (tools/iff_stroths_census.py over game-data/The
        // Sims: STR# 5430 chunks / 155482 blocks, TTAs 1568 chunks / 11286 blocks - per member
        // basename LAST-wins, STR.Read mirror fmt 0/-1/-2 all, fmt -3 lang 0/1). IFF data -
        // pie-menu + behavior strings are IFF-driven.
        public const int StrSharpBlockCensus = 155482;
        public const int StrSharpChunkCensus = 5430;
        public const int TTAsBlockCensus = 11286;
        public const int TTAsChunkCensus = 1568;

        // Round 59 IFF-LITERAL constant-table census (tools/iff_consts_census.py over game-data/The
        // Sims: BCON 3663 chunks / 29952 blocks, TPRP 206 chunks / 434 blocks, GLOB 478 chunks -
        // engine BCON/TPRP/GLOB Read mirrors, basename LAST-wins; GLOB is INSTANCE count (both
        // "GLOB" and lowercase "glob": NPC_Superstar_PA / NPC_Vacation_Director each carry both
        // spellings, id 128), BCON/TPRP distinct (type,id). IFF data - BHAVs read these IFF-literally.
        public const int BconChunkCensus = 3663;
        public const int BconBlockCensus = 29952;
        public const int TprpChunkCensus = 206;
        public const int TprpBlockCensus = 434;
        public const int GlobChunkCensus = 478;

        // Round 60 IFF-LITERAL BHAV census (tools/iff_bhav_census.py over game-data/The Sims): 388503
        // instruction blocks across 1159 basename-keyed members - count mirrors engine BHAV.Read
        // (0x8002 header u16 at payload+2, 12-byte instructions). IFF data - the instructions the
        // behavior VM executes are IFF-literally IFF data.
        public const int BhavInstructionCensus = 388503;

        // Round 62 IFF-LITERAL DGRP/PALT census (tools/iff_dgrp_palt_census.py over game-data/The
        // Sims): DGRP draw-group sprite linkage (514716 sprite refs / 839 members) and PALT palette
        // entries (1340928 / 840 members), per member basename LAST-wins, chunks distinct by (type,id),
        // engine DGRP.Read (20004+) / PALT.Read mirrors. IFF data - the sprite linkage and palette
        // entries the renderer consumes.
        // DGRP/PALT chunk INSTANCES (engine IffFile.List<DGRP>/List<PALT> counts chunks, not members):
        // DGRP 20522 chunks / 514716 sprite refs, PALT 5238 chunks / 1340928 entries.
        public const int DgrpChunkCensus = 20522;
        public const int DgrpSpriteRefCensus = 514716;
        public const int PaltChunkCensus = 5238;
        public const int PaltEntryCensus = 1340928;

        // Round 63 IFF-LITERAL SLOT/SPR#/SPR2 census (tools/iff_slot_spr_census.py over game-data/
        // The Sims): SLOT 1472 chunks / 6969 slot entries (engine SLOT.Read numSlots u32 at +12;
        // SPR# 758 chunks / 3120 frames (spriteCount u32 at +4, BE if v1==0; corpus 504/511);
        // SPR2 17140 chunks / 138948 frames (spriteCount u32 at +4 for v1000, +8 for v1001; corpus
        // 1000). Per member basename LAST-wins, chunk instances. IFF data - object slot tables and
        // sprite-frame surfaces the engine mounts.

        // Round 64 IFF-LITERAL BHAV operand-byte census (tools/iff_bhav_operands.py over
        // game-data/The Sims): 8-byte operand per instruction, histogram over 256 byte values
        // = IFF data - the literal operand bytes the behavior VM reads per instruction.

        // Round 65 IFF-LITERAL REGISTERED-CHUNK INVENTORY census (tools/iff_chunks_census.py
        // over game-data/The Sims): per mounted member (basename LAST-wins), every chunk whose
        // raw 4-char type is in CHUNK_TYPES is instantiated by IffFile.AddChunk and enumerated
        // by ListAll(); unregistered types are skipped. Raw spellings kept. IFF data.
        public static readonly string[] ChunkTypeCensus = new string[] { "BCON=3663", "BHAV=27852", "BMP_=6174", "CARR=21", "CATS=1", "CTSS=1895", "DGRP=20522", "FCNS=6", "FWAV=15921", "GLOB=476", "OBJD=5120", "OBJf=3906", "PALT=5238", "POSI=41", "SLOT=1472", "SPR#=758", "SPR2=17140", "STR#=5430", "TMPL=4", "TPRP=206", "TRCN=9", "TREE=213", "TTAB=1804", "TTAs=1568", "XXXX=135", "glob=2", "pers=2", "rsmp=1200" };
        public static readonly int[] BhavOperandByteCensus = new int[] { 1447208, 171243, 124693, 78187, 31177, 140398, 23520, 149374, 61032, 37032, 58259, 17599, 10305, 6324, 11610, 9637, 9038, 4689, 17154, 6083, 6416, 2537, 1351, 1458, 1595, 73802, 41049, 4732, 2149, 2443, 2556, 1555, 14654, 2245, 2306, 1280, 957, 1251, 618, 1554, 2607, 1247, 1239, 1095, 1544, 1252, 2064, 3136, 768, 865, 2415, 745, 710, 649, 709, 842, 1007, 752, 1300, 1261, 880, 1650, 744, 648, 2417, 2173, 2315, 1377, 1133, 487, 668, 950, 588, 494, 518, 626, 415, 909, 1263, 496, 889, 544, 442, 475, 402, 413, 422, 228, 783, 423, 783, 324, 587, 327, 373, 291, 604, 290, 316, 359, 6465, 2364, 1197, 546, 419, 322, 257, 245, 300, 261, 383, 505, 392, 487, 237, 311, 334, 368, 372, 451, 423, 165, 356, 356, 357, 210, 220, 332, 3936, 3038, 2566, 4146, 3914, 1337, 1573, 1056, 1311, 1215, 630, 612, 671, 558, 641, 405, 790, 433, 413, 356, 441, 407, 1001, 287, 307, 629, 440, 337, 734, 254, 499, 437, 436, 268, 315, 634, 348, 290, 455, 347, 234, 415, 198, 509, 292, 277, 280, 361, 360, 290, 201, 395, 469, 271, 261, 181, 326, 400, 429, 253, 638, 177, 415, 345, 286, 518, 345, 331, 329, 374, 253, 236, 972, 428, 211, 286, 243, 238, 470, 403, 267, 248, 276, 366, 297, 197, 201, 341, 433, 358, 188, 209, 346, 269, 151, 315, 271, 254, 294, 429, 212, 168, 304, 406, 1992, 227, 458, 197, 731, 193, 281, 358, 156, 233, 193, 379, 1342, 167, 434, 367, 229, 364, 579, 402, 524, 463, 2301, 380349 };
        public static readonly Dictionary<int, int> BhavOperandByOpcode =
            new Dictionary<int, int>
            {
                { 0, 28 }, // op=0x0 byte=0
                { 255, 12 }, // op=0x0 byte=255
                { 256, 4713 }, // op=0x1 byte=0
                { 257, 3 }, // op=0x1 byte=1
                { 258, 55 }, // op=0x1 byte=2
                { 259, 12 }, // op=0x1 byte=3
                { 260, 53 }, // op=0x1 byte=4
                { 261, 16 }, // op=0x1 byte=5
                { 262, 22 }, // op=0x1 byte=6
                { 266, 26 }, // op=0x1 byte=10
                { 267, 31 }, // op=0x1 byte=11
                { 268, 47 }, // op=0x1 byte=12
                { 269, 2 }, // op=0x1 byte=13
                { 270, 18 }, // op=0x1 byte=14
                { 271, 231 }, // op=0x1 byte=15
                { 273, 15 }, // op=0x1 byte=17
                { 274, 2 }, // op=0x1 byte=18
                { 275, 3 }, // op=0x1 byte=19
                { 276, 8 }, // op=0x1 byte=20
                { 277, 40 }, // op=0x1 byte=21
                { 278, 1 }, // op=0x1 byte=22
                { 279, 10 }, // op=0x1 byte=23
                { 280, 13 }, // op=0x1 byte=24
                { 281, 15 }, // op=0x1 byte=25
                { 282, 4 }, // op=0x1 byte=26
                { 283, 3 }, // op=0x1 byte=27
                { 284, 5 }, // op=0x1 byte=28
                { 285, 1 }, // op=0x1 byte=29
                { 286, 1 }, // op=0x1 byte=30
                { 288, 1 }, // op=0x1 byte=32
                { 289, 3 }, // op=0x1 byte=33
                { 290, 4 }, // op=0x1 byte=34
                { 291, 2 }, // op=0x1 byte=35
                { 292, 4 }, // op=0x1 byte=36
                { 294, 4 }, // op=0x1 byte=38
                { 295, 2 }, // op=0x1 byte=39
                { 296, 1 }, // op=0x1 byte=40
                { 297, 10 }, // op=0x1 byte=41
                { 298, 1 }, // op=0x1 byte=42
                { 299, 2 }, // op=0x1 byte=43
                { 512, 832664 }, // op=0x2 byte=0
                { 513, 121200 }, // op=0x2 byte=1
                { 514, 93844 }, // op=0x2 byte=2
                { 515, 57375 }, // op=0x2 byte=3
                { 516, 24429 }, // op=0x2 byte=4
                { 517, 135008 }, // op=0x2 byte=5
                { 518, 16520 }, // op=0x2 byte=6
                { 519, 138703 }, // op=0x2 byte=7
                { 520, 53216 }, // op=0x2 byte=8
                { 521, 32135 }, // op=0x2 byte=9
                { 522, 43499 }, // op=0x2 byte=10
                { 523, 13337 }, // op=0x2 byte=11
                { 524, 3243 }, // op=0x2 byte=12
                { 525, 3806 }, // op=0x2 byte=13
                { 526, 10193 }, // op=0x2 byte=14
                { 527, 7407 }, // op=0x2 byte=15
                { 528, 5896 }, // op=0x2 byte=16
                { 529, 2295 }, // op=0x2 byte=17
                { 530, 16037 }, // op=0x2 byte=18
                { 531, 5310 }, // op=0x2 byte=19
                { 532, 4377 }, // op=0x2 byte=20
                { 533, 1601 }, // op=0x2 byte=21
                { 534, 739 }, // op=0x2 byte=22
                { 535, 630 }, // op=0x2 byte=23
                { 536, 885 }, // op=0x2 byte=24
                { 537, 69467 }, // op=0x2 byte=25
                { 538, 37476 }, // op=0x2 byte=26
                { 539, 4150 }, // op=0x2 byte=27
                { 540, 1625 }, // op=0x2 byte=28
                { 541, 1922 }, // op=0x2 byte=29
                { 542, 1218 }, // op=0x2 byte=30
                { 543, 790 }, // op=0x2 byte=31
                { 544, 2826 }, // op=0x2 byte=32
                { 545, 1574 }, // op=0x2 byte=33
                { 546, 1581 }, // op=0x2 byte=34
                { 547, 803 }, // op=0x2 byte=35
                { 548, 437 }, // op=0x2 byte=36
                { 549, 815 }, // op=0x2 byte=37
                { 550, 344 }, // op=0x2 byte=38
                { 551, 577 }, // op=0x2 byte=39
                { 552, 2015 }, // op=0x2 byte=40
                { 553, 426 }, // op=0x2 byte=41
                { 554, 762 }, // op=0x2 byte=42
                { 555, 652 }, // op=0x2 byte=43
                { 556, 580 }, // op=0x2 byte=44
                { 557, 585 }, // op=0x2 byte=45
                { 558, 440 }, // op=0x2 byte=46
                { 559, 277 }, // op=0x2 byte=47
                { 560, 256 }, // op=0x2 byte=48
                { 561, 249 }, // op=0x2 byte=49
                { 562, 1265 }, // op=0x2 byte=50
                { 563, 171 }, // op=0x2 byte=51
                { 564, 232 }, // op=0x2 byte=52
                { 565, 228 }, // op=0x2 byte=53
                { 566, 356 }, // op=0x2 byte=54
                { 567, 221 }, // op=0x2 byte=55
                { 568, 421 }, // op=0x2 byte=56
                { 569, 292 }, // op=0x2 byte=57
                { 570, 934 }, // op=0x2 byte=58
                { 571, 809 }, // op=0x2 byte=59
                { 572, 268 }, // op=0x2 byte=60
                { 573, 1255 }, // op=0x2 byte=61
                { 574, 284 }, // op=0x2 byte=62
                { 575, 237 }, // op=0x2 byte=63
                { 576, 1702 }, // op=0x2 byte=64
                { 577, 1853 }, // op=0x2 byte=65
                { 578, 1881 }, // op=0x2 byte=66
                { 579, 891 }, // op=0x2 byte=67
                { 580, 837 }, // op=0x2 byte=68
                { 581, 127 }, // op=0x2 byte=69
                { 582, 276 }, // op=0x2 byte=70
                { 583, 420 }, // op=0x2 byte=71
                { 584, 198 }, // op=0x2 byte=72
                { 585, 158 }, // op=0x2 byte=73
                { 586, 109 }, // op=0x2 byte=74
                { 587, 265 }, // op=0x2 byte=75
                { 588, 104 }, // op=0x2 byte=76
                { 589, 518 }, // op=0x2 byte=77
                { 590, 74 }, // op=0x2 byte=78
                { 591, 56 }, // op=0x2 byte=79
                { 592, 397 }, // op=0x2 byte=80
                { 593, 216 }, // op=0x2 byte=81
                { 594, 29 }, // op=0x2 byte=82
                { 595, 21 }, // op=0x2 byte=83
                { 596, 43 }, // op=0x2 byte=84
                { 597, 103 }, // op=0x2 byte=85
                { 598, 13 }, // op=0x2 byte=86
                { 599, 22 }, // op=0x2 byte=87
                { 600, 459 }, // op=0x2 byte=88
                { 601, 39 }, // op=0x2 byte=89
                { 602, 159 }, // op=0x2 byte=90
                { 603, 8 }, // op=0x2 byte=91
                { 604, 23 }, // op=0x2 byte=92
                { 605, 29 }, // op=0x2 byte=93
                { 606, 37 }, // op=0x2 byte=94
                { 607, 42 }, // op=0x2 byte=95
                { 608, 117 }, // op=0x2 byte=96
                { 609, 10 }, // op=0x2 byte=97
                { 610, 14 }, // op=0x2 byte=98
                { 611, 52 }, // op=0x2 byte=99
                { 612, 3374 }, // op=0x2 byte=100
                { 613, 2057 }, // op=0x2 byte=101
                { 614, 828 }, // op=0x2 byte=102
                { 615, 172 }, // op=0x2 byte=103
                { 616, 126 }, // op=0x2 byte=104
                { 617, 98 }, // op=0x2 byte=105
                { 618, 30 }, // op=0x2 byte=106
                { 619, 33 }, // op=0x2 byte=107
                { 620, 7 }, // op=0x2 byte=108
                { 621, 13 }, // op=0x2 byte=109
                { 622, 141 }, // op=0x2 byte=110
                { 623, 87 }, // op=0x2 byte=111
                { 624, 19 }, // op=0x2 byte=112
                { 625, 15 }, // op=0x2 byte=113
                { 626, 1 }, // op=0x2 byte=114
                { 627, 3 }, // op=0x2 byte=115
                { 628, 3 }, // op=0x2 byte=116
                { 629, 3 }, // op=0x2 byte=117
                { 630, 5 }, // op=0x2 byte=118
                { 631, 14 }, // op=0x2 byte=119
                { 632, 50 }, // op=0x2 byte=120
                { 633, 13 }, // op=0x2 byte=121
                { 634, 7 }, // op=0x2 byte=122
                { 635, 3 }, // op=0x2 byte=123
                { 636, 10 }, // op=0x2 byte=124
                { 637, 16 }, // op=0x2 byte=125
                { 638, 1 }, // op=0x2 byte=126
                { 639, 12 }, // op=0x2 byte=127
                { 640, 2842 }, // op=0x2 byte=128
                { 641, 1857 }, // op=0x2 byte=129
                { 642, 1872 }, // op=0x2 byte=130
                { 643, 1393 }, // op=0x2 byte=131
                { 644, 1323 }, // op=0x2 byte=132
                { 645, 845 }, // op=0x2 byte=133
                { 646, 964 }, // op=0x2 byte=134
                { 647, 610 }, // op=0x2 byte=135
                { 648, 519 }, // op=0x2 byte=136
                { 649, 445 }, // op=0x2 byte=137
                { 650, 344 }, // op=0x2 byte=138
                { 651, 309 }, // op=0x2 byte=139
                { 652, 285 }, // op=0x2 byte=140
                { 653, 277 }, // op=0x2 byte=141
                { 654, 171 }, // op=0x2 byte=142
                { 655, 121 }, // op=0x2 byte=143
                { 656, 285 }, // op=0x2 byte=144
                { 657, 118 }, // op=0x2 byte=145
                { 658, 108 }, // op=0x2 byte=146
                { 659, 139 }, // op=0x2 byte=147
                { 660, 68 }, // op=0x2 byte=148
                { 661, 44 }, // op=0x2 byte=149
                { 662, 398 }, // op=0x2 byte=150
                { 663, 111 }, // op=0x2 byte=151
                { 664, 73 }, // op=0x2 byte=152
                { 665, 132 }, // op=0x2 byte=153
                { 666, 92 }, // op=0x2 byte=154
                { 667, 97 }, // op=0x2 byte=155
                { 668, 404 }, // op=0x2 byte=156
                { 669, 48 }, // op=0x2 byte=157
                { 670, 141 }, // op=0x2 byte=158
                { 671, 133 }, // op=0x2 byte=159
                { 672, 67 }, // op=0x2 byte=160
                { 673, 70 }, // op=0x2 byte=161
                { 674, 52 }, // op=0x2 byte=162
                { 675, 279 }, // op=0x2 byte=163
                { 676, 81 }, // op=0x2 byte=164
                { 677, 47 }, // op=0x2 byte=165
                { 678, 228 }, // op=0x2 byte=166
                { 679, 20 }, // op=0x2 byte=167
                { 680, 21 }, // op=0x2 byte=168
                { 681, 22 }, // op=0x2 byte=169
                { 682, 23 }, // op=0x2 byte=170
                { 683, 73 }, // op=0x2 byte=171
                { 684, 12 }, // op=0x2 byte=172
                { 685, 10 }, // op=0x2 byte=173
                { 686, 17 }, // op=0x2 byte=174
                { 687, 22 }, // op=0x2 byte=175
                { 688, 107 }, // op=0x2 byte=176
                { 689, 7 }, // op=0x2 byte=177
                { 690, 10 }, // op=0x2 byte=178
                { 691, 15 }, // op=0x2 byte=179
                { 692, 12 }, // op=0x2 byte=180
                { 693, 26 }, // op=0x2 byte=181
                { 694, 19 }, // op=0x2 byte=182
                { 695, 15 }, // op=0x2 byte=183
                { 696, 65 }, // op=0x2 byte=184
                { 697, 8 }, // op=0x2 byte=185
                { 698, 62 }, // op=0x2 byte=186
                { 699, 6 }, // op=0x2 byte=187
                { 700, 241 }, // op=0x2 byte=188
                { 701, 6 }, // op=0x2 byte=189
                { 702, 11 }, // op=0x2 byte=190
                { 703, 20 }, // op=0x2 byte=191
                { 704, 9 }, // op=0x2 byte=192
                { 705, 4 }, // op=0x2 byte=193
                { 706, 13 }, // op=0x2 byte=194
                { 707, 3 }, // op=0x2 byte=195
                { 708, 95 }, // op=0x2 byte=196
                { 709, 3 }, // op=0x2 byte=197
                { 710, 5 }, // op=0x2 byte=198
                { 711, 8 }, // op=0x2 byte=199
                { 712, 561 }, // op=0x2 byte=200
                { 713, 59 }, // op=0x2 byte=201
                { 714, 46 }, // op=0x2 byte=202
                { 715, 50 }, // op=0x2 byte=203
                { 716, 14 }, // op=0x2 byte=204
                { 717, 27 }, // op=0x2 byte=205
                { 718, 166 }, // op=0x2 byte=206
                { 719, 6 }, // op=0x2 byte=207
                { 720, 92 }, // op=0x2 byte=208
                { 721, 1 }, // op=0x2 byte=209
                { 722, 34 }, // op=0x2 byte=210
                { 723, 11 }, // op=0x2 byte=211
                { 724, 4 }, // op=0x2 byte=212
                { 725, 1 }, // op=0x2 byte=213
                { 726, 1 }, // op=0x2 byte=214
                { 727, 3 }, // op=0x2 byte=215
                { 728, 77 }, // op=0x2 byte=216
                { 729, 2 }, // op=0x2 byte=217
                { 730, 3 }, // op=0x2 byte=218
                { 731, 9 }, // op=0x2 byte=219
                { 732, 108 }, // op=0x2 byte=220
                { 733, 42 }, // op=0x2 byte=221
                { 734, 21 }, // op=0x2 byte=222
                { 735, 47 }, // op=0x2 byte=223
                { 736, 10 }, // op=0x2 byte=224
                { 737, 4 }, // op=0x2 byte=225
                { 738, 98 }, // op=0x2 byte=226
                { 740, 4 }, // op=0x2 byte=228
                { 741, 2 }, // op=0x2 byte=229
                { 742, 17 }, // op=0x2 byte=230
                { 743, 179 }, // op=0x2 byte=231
                { 744, 982 }, // op=0x2 byte=232
                { 748, 146 }, // op=0x2 byte=236
                { 750, 78 }, // op=0x2 byte=238
                { 751, 1 }, // op=0x2 byte=239
                { 752, 28 }, // op=0x2 byte=240
                { 753, 11 }, // op=0x2 byte=241
                { 754, 1 }, // op=0x2 byte=242
                { 755, 3 }, // op=0x2 byte=243
                { 756, 763 }, // op=0x2 byte=244
                { 757, 3 }, // op=0x2 byte=245
                { 758, 98 }, // op=0x2 byte=246
                { 759, 57 }, // op=0x2 byte=247
                { 760, 17 }, // op=0x2 byte=248
                { 762, 446 }, // op=0x2 byte=250
                { 763, 30 }, // op=0x2 byte=251
                { 764, 39 }, // op=0x2 byte=252
                { 765, 14 }, // op=0x2 byte=253
                { 766, 102 }, // op=0x2 byte=254
                { 767, 12192 }, // op=0x2 byte=255
                { 768, 256 }, // op=0x3 byte=0
                { 1024, 1752 }, // op=0x4 byte=0
                { 1279, 200 }, // op=0x4 byte=255
                { 1280, 96 }, // op=0x5 byte=0
                { 1535, 8 }, // op=0x5 byte=255
                { 1536, 35622 }, // op=0x6 byte=0
                { 1537, 4523 }, // op=0x6 byte=1
                { 1538, 6650 }, // op=0x6 byte=2
                { 1539, 414 }, // op=0x6 byte=3
                { 1540, 337 }, // op=0x6 byte=4
                { 1541, 270 }, // op=0x6 byte=5
                { 1542, 210 }, // op=0x6 byte=6
                { 1543, 185 }, // op=0x6 byte=7
                { 1544, 185 }, // op=0x6 byte=8
                { 1545, 121 }, // op=0x6 byte=9
                { 1546, 140 }, // op=0x6 byte=10
                { 1547, 174 }, // op=0x6 byte=11
                { 1548, 179 }, // op=0x6 byte=12
                { 1549, 129 }, // op=0x6 byte=13
                { 1550, 121 }, // op=0x6 byte=14
                { 1551, 60 }, // op=0x6 byte=15
                { 1552, 103 }, // op=0x6 byte=16
                { 1553, 91 }, // op=0x6 byte=17
                { 1554, 83 }, // op=0x6 byte=18
                { 1555, 45 }, // op=0x6 byte=19
                { 1556, 29 }, // op=0x6 byte=20
                { 1557, 30 }, // op=0x6 byte=21
                { 1558, 25 }, // op=0x6 byte=22
                { 1559, 23 }, // op=0x6 byte=23
                { 1560, 18 }, // op=0x6 byte=24
                { 1561, 13 }, // op=0x6 byte=25
                { 1562, 15 }, // op=0x6 byte=26
                { 1563, 18 }, // op=0x6 byte=27
                { 1564, 41 }, // op=0x6 byte=28
                { 1565, 29 }, // op=0x6 byte=29
                { 1566, 18 }, // op=0x6 byte=30
                { 1567, 18 }, // op=0x6 byte=31
                { 1568, 20 }, // op=0x6 byte=32
                { 1569, 18 }, // op=0x6 byte=33
                { 1570, 8 }, // op=0x6 byte=34
                { 1571, 8 }, // op=0x6 byte=35
                { 1572, 8 }, // op=0x6 byte=36
                { 1573, 8 }, // op=0x6 byte=37
                { 1574, 8 }, // op=0x6 byte=38
                { 1575, 8 }, // op=0x6 byte=39
                { 1576, 8 }, // op=0x6 byte=40
                { 1577, 9 }, // op=0x6 byte=41
                { 1578, 6 }, // op=0x6 byte=42
                { 1580, 6 }, // op=0x6 byte=44
                { 1581, 6 }, // op=0x6 byte=45
                { 1582, 6 }, // op=0x6 byte=46
                { 1583, 6 }, // op=0x6 byte=47
                { 1584, 6 }, // op=0x6 byte=48
                { 1585, 6 }, // op=0x6 byte=49
                { 1791, 8 }, // op=0x6 byte=255
                { 1792, 20017 }, // op=0x7 byte=0
                { 1793, 1730 }, // op=0x7 byte=1
                { 1794, 773 }, // op=0x7 byte=2
                { 2047, 96 }, // op=0x7 byte=255
                { 2048, 21795 }, // op=0x8 byte=0
                { 2049, 1180 }, // op=0x8 byte=1
                { 2050, 802 }, // op=0x8 byte=2
                { 2051, 1288 }, // op=0x8 byte=3
                { 2052, 422 }, // op=0x8 byte=4
                { 2053, 286 }, // op=0x8 byte=5
                { 2054, 178 }, // op=0x8 byte=6
                { 2055, 4237 }, // op=0x8 byte=7
                { 2056, 2706 }, // op=0x8 byte=8
                { 2057, 261 }, // op=0x8 byte=9
                { 2058, 162 }, // op=0x8 byte=10
                { 2059, 199 }, // op=0x8 byte=11
                { 2060, 16 }, // op=0x8 byte=12
                { 2061, 10 }, // op=0x8 byte=13
                { 2062, 124 }, // op=0x8 byte=14
                { 2063, 67 }, // op=0x8 byte=15
                { 2064, 63 }, // op=0x8 byte=16
                { 2065, 16 }, // op=0x8 byte=17
                { 2066, 166 }, // op=0x8 byte=18
                { 2067, 17 }, // op=0x8 byte=19
                { 2068, 119 }, // op=0x8 byte=20
                { 2069, 8 }, // op=0x8 byte=21
                { 2070, 3 }, // op=0x8 byte=22
                { 2071, 2 }, // op=0x8 byte=23
                { 2072, 12 }, // op=0x8 byte=24
                { 2073, 1921 }, // op=0x8 byte=25
                { 2074, 333 }, // op=0x8 byte=26
                { 2075, 4 }, // op=0x8 byte=27
                { 2076, 2 }, // op=0x8 byte=28
                { 2077, 26 }, // op=0x8 byte=29
                { 2078, 26 }, // op=0x8 byte=30
                { 2079, 2 }, // op=0x8 byte=31
                { 2080, 6 }, // op=0x8 byte=32
                { 2081, 50 }, // op=0x8 byte=33
                { 2082, 1 }, // op=0x8 byte=34
                { 2083, 1 }, // op=0x8 byte=35
                { 2084, 3 }, // op=0x8 byte=36
                { 2085, 2 }, // op=0x8 byte=37
                { 2086, 1 }, // op=0x8 byte=38
                { 2087, 17 }, // op=0x8 byte=39
                { 2088, 30 }, // op=0x8 byte=40
                { 2089, 5 }, // op=0x8 byte=41
                { 2091, 3 }, // op=0x8 byte=43
                { 2092, 11 }, // op=0x8 byte=44
                { 2096, 2 }, // op=0x8 byte=48
                { 2098, 34 }, // op=0x8 byte=50
                { 2100, 2 }, // op=0x8 byte=52
                { 2105, 13 }, // op=0x8 byte=57
                { 2108, 10 }, // op=0x8 byte=60
                { 2112, 3 }, // op=0x8 byte=64
                { 2113, 1 }, // op=0x8 byte=65
                { 2118, 3 }, // op=0x8 byte=70
                { 2120, 1 }, // op=0x8 byte=72
                { 2123, 3 }, // op=0x8 byte=75
                { 2124, 1 }, // op=0x8 byte=76
                { 2126, 1 }, // op=0x8 byte=78
                { 2128, 10 }, // op=0x8 byte=80
                { 2131, 1 }, // op=0x8 byte=83
                { 2135, 1 }, // op=0x8 byte=87
                { 2136, 3 }, // op=0x8 byte=88
                { 2139, 2 }, // op=0x8 byte=91
                { 2144, 1 }, // op=0x8 byte=96
                { 2146, 1 }, // op=0x8 byte=98
                { 2148, 846 }, // op=0x8 byte=100
                { 2149, 17 }, // op=0x8 byte=101
                { 2153, 9 }, // op=0x8 byte=105
                { 2159, 2 }, // op=0x8 byte=111
                { 2163, 2 }, // op=0x8 byte=115
                { 2165, 8 }, // op=0x8 byte=117
                { 2168, 15 }, // op=0x8 byte=120
                { 2173, 1 }, // op=0x8 byte=125
                { 2174, 1 }, // op=0x8 byte=126
                { 2176, 2 }, // op=0x8 byte=128
                { 2177, 17 }, // op=0x8 byte=129
                { 2178, 14 }, // op=0x8 byte=130
                { 2179, 12 }, // op=0x8 byte=131
                { 2180, 8 }, // op=0x8 byte=132
                { 2182, 3 }, // op=0x8 byte=134
                { 2183, 7 }, // op=0x8 byte=135
                { 2184, 17 }, // op=0x8 byte=136
                { 2185, 5 }, // op=0x8 byte=137
                { 2186, 3 }, // op=0x8 byte=138
                { 2187, 5 }, // op=0x8 byte=139
                { 2188, 21 }, // op=0x8 byte=140
                { 2189, 5 }, // op=0x8 byte=141
                { 2190, 3 }, // op=0x8 byte=142
                { 2191, 3 }, // op=0x8 byte=143
                { 2192, 8 }, // op=0x8 byte=144
                { 2193, 3 }, // op=0x8 byte=145
                { 2196, 2 }, // op=0x8 byte=148
                { 2197, 2 }, // op=0x8 byte=149
                { 2198, 7 }, // op=0x8 byte=150
                { 2200, 1 }, // op=0x8 byte=152
                { 2201, 2 }, // op=0x8 byte=153
                { 2208, 1 }, // op=0x8 byte=160
                { 2209, 1 }, // op=0x8 byte=161
                { 2212, 1 }, // op=0x8 byte=164
                { 2224, 3 }, // op=0x8 byte=176
                { 2230, 1 }, // op=0x8 byte=182
                { 2232, 3 }, // op=0x8 byte=184
                { 2236, 1 }, // op=0x8 byte=188
                { 2244, 1 }, // op=0x8 byte=196
                { 2248, 2 }, // op=0x8 byte=200
                { 2249, 4 }, // op=0x8 byte=201
                { 2254, 1 }, // op=0x8 byte=206
                { 2256, 1 }, // op=0x8 byte=208
                { 2257, 1 }, // op=0x8 byte=209
                { 2280, 467 }, // op=0x8 byte=232
                { 2281, 2 }, // op=0x8 byte=233
                { 2283, 2 }, // op=0x8 byte=235
                { 2292, 5 }, // op=0x8 byte=244
                { 2304, 186 }, // op=0x9 byte=0
                { 2305, 9 }, // op=0x9 byte=1
                { 2309, 1 }, // op=0x9 byte=5
                { 2559, 12 }, // op=0x9 byte=255
                { 2560, 95 }, // op=0xa byte=0
                { 2561, 9 }, // op=0xa byte=1
                { 2816, 1881 }, // op=0xb byte=0
                { 2817, 326 }, // op=0xb byte=1
                { 2818, 13 }, // op=0xb byte=2
                { 2819, 247 }, // op=0xb byte=3
                { 2820, 3 }, // op=0xb byte=4
                { 2822, 2 }, // op=0xb byte=6
                { 2824, 10 }, // op=0xb byte=8
                { 2825, 2 }, // op=0xb byte=9
                { 2826, 2 }, // op=0xb byte=10
                { 2827, 243 }, // op=0xb byte=11
                { 2834, 7 }, // op=0xb byte=18
                { 2841, 36 }, // op=0xb byte=25
                { 2893, 7 }, // op=0xb byte=77
                { 3071, 21 }, // op=0xb byte=255
                { 3072, 681 }, // op=0xc byte=0
                { 3073, 208 }, // op=0xc byte=1
                { 3074, 6 }, // op=0xc byte=2
                { 3075, 89 }, // op=0xc byte=3
                { 3077, 2 }, // op=0xc byte=5
                { 3080, 67 }, // op=0xc byte=8
                { 3081, 1 }, // op=0xc byte=9
                { 3083, 83 }, // op=0xc byte=11
                { 3097, 135 }, // op=0xc byte=25
                { 3328, 10997 }, // op=0xd byte=0
                { 3329, 1563 }, // op=0xd byte=1
                { 3330, 1401 }, // op=0xd byte=2
                { 3331, 2222 }, // op=0xd byte=3
                { 3332, 219 }, // op=0xd byte=4
                { 3333, 102 }, // op=0xd byte=5
                { 3334, 107 }, // op=0xd byte=6
                { 3335, 382 }, // op=0xd byte=7
                { 3336, 37 }, // op=0xd byte=8
                { 3337, 53 }, // op=0xd byte=9
                { 3338, 172 }, // op=0xd byte=10
                { 3339, 287 }, // op=0xd byte=11
                { 3340, 35 }, // op=0xd byte=12
                { 3341, 13 }, // op=0xd byte=13
                { 3342, 57 }, // op=0xd byte=14
                { 3343, 161 }, // op=0xd byte=15
                { 3344, 39 }, // op=0xd byte=16
                { 3345, 8 }, // op=0xd byte=17
                { 3346, 11 }, // op=0xd byte=18
                { 3347, 12 }, // op=0xd byte=19
                { 3348, 19 }, // op=0xd byte=20
                { 3349, 19 }, // op=0xd byte=21
                { 3350, 13 }, // op=0xd byte=22
                { 3351, 16 }, // op=0xd byte=23
                { 3352, 22 }, // op=0xd byte=24
                { 3353, 18 }, // op=0xd byte=25
                { 3354, 13 }, // op=0xd byte=26
                { 3355, 12 }, // op=0xd byte=27
                { 3356, 14 }, // op=0xd byte=28
                { 3357, 31 }, // op=0xd byte=29
                { 3358, 27 }, // op=0xd byte=30
                { 3359, 18 }, // op=0xd byte=31
                { 3360, 16 }, // op=0xd byte=32
                { 3361, 14 }, // op=0xd byte=33
                { 3362, 10 }, // op=0xd byte=34
                { 3363, 11 }, // op=0xd byte=35
                { 3364, 10 }, // op=0xd byte=36
                { 3365, 4 }, // op=0xd byte=37
                { 3366, 4 }, // op=0xd byte=38
                { 3367, 9 }, // op=0xd byte=39
                { 3368, 9 }, // op=0xd byte=40
                { 3369, 5 }, // op=0xd byte=41
                { 3370, 5 }, // op=0xd byte=42
                { 3371, 16 }, // op=0xd byte=43
                { 3372, 16 }, // op=0xd byte=44
                { 3373, 41 }, // op=0xd byte=45
                { 3374, 7 }, // op=0xd byte=46
                { 3375, 7 }, // op=0xd byte=47
                { 3376, 13 }, // op=0xd byte=48
                { 3377, 13 }, // op=0xd byte=49
                { 3378, 14 }, // op=0xd byte=50
                { 3379, 17 }, // op=0xd byte=51
                { 3380, 15 }, // op=0xd byte=52
                { 3381, 10 }, // op=0xd byte=53
                { 3382, 10 }, // op=0xd byte=54
                { 3383, 24 }, // op=0xd byte=55
                { 3384, 12 }, // op=0xd byte=56
                { 3385, 9 }, // op=0xd byte=57
                { 3386, 17 }, // op=0xd byte=58
                { 3387, 12 }, // op=0xd byte=59
                { 3388, 7 }, // op=0xd byte=60
                { 3389, 7 }, // op=0xd byte=61
                { 3390, 17 }, // op=0xd byte=62
                { 3391, 17 }, // op=0xd byte=63
                { 3392, 8 }, // op=0xd byte=64
                { 3393, 8 }, // op=0xd byte=65
                { 3394, 6 }, // op=0xd byte=66
                { 3395, 5 }, // op=0xd byte=67
                { 3396, 2 }, // op=0xd byte=68
                { 3397, 1 }, // op=0xd byte=69
                { 3398, 3 }, // op=0xd byte=70
                { 3399, 3 }, // op=0xd byte=71
                { 3400, 5 }, // op=0xd byte=72
                { 3401, 6 }, // op=0xd byte=73
                { 3402, 1 }, // op=0xd byte=74
                { 3403, 1 }, // op=0xd byte=75
                { 3404, 8 }, // op=0xd byte=76
                { 3405, 8 }, // op=0xd byte=77
                { 3406, 6 }, // op=0xd byte=78
                { 3407, 6 }, // op=0xd byte=79
                { 3408, 6 }, // op=0xd byte=80
                { 3409, 6 }, // op=0xd byte=81
                { 3410, 8 }, // op=0xd byte=82
                { 3411, 8 }, // op=0xd byte=83
                { 3412, 6 }, // op=0xd byte=84
                { 3413, 5 }, // op=0xd byte=85
                { 3414, 7 }, // op=0xd byte=86
                { 3415, 7 }, // op=0xd byte=87
                { 3416, 3 }, // op=0xd byte=88
                { 3417, 2 }, // op=0xd byte=89
                { 3418, 4 }, // op=0xd byte=90
                { 3419, 4 }, // op=0xd byte=91
                { 3420, 4 }, // op=0xd byte=92
                { 3421, 4 }, // op=0xd byte=93
                { 3422, 11 }, // op=0xd byte=94
                { 3423, 12 }, // op=0xd byte=95
                { 3424, 4 }, // op=0xd byte=96
                { 3425, 2 }, // op=0xd byte=97
                { 3426, 4 }, // op=0xd byte=98
                { 3427, 4 }, // op=0xd byte=99
                { 3428, 3 }, // op=0xd byte=100
                { 3429, 3 }, // op=0xd byte=101
                { 3430, 3 }, // op=0xd byte=102
                { 3431, 3 }, // op=0xd byte=103
                { 3432, 5 }, // op=0xd byte=104
                { 3433, 5 }, // op=0xd byte=105
                { 3434, 4 }, // op=0xd byte=106
                { 3435, 4 }, // op=0xd byte=107
                { 3436, 4 }, // op=0xd byte=108
                { 3437, 5 }, // op=0xd byte=109
                { 3438, 8 }, // op=0xd byte=110
                { 3439, 8 }, // op=0xd byte=111
                { 3440, 4 }, // op=0xd byte=112
                { 3441, 2 }, // op=0xd byte=113
                { 3442, 6 }, // op=0xd byte=114
                { 3443, 6 }, // op=0xd byte=115
                { 3444, 4 }, // op=0xd byte=116
                { 3445, 4 }, // op=0xd byte=117
                { 3446, 4 }, // op=0xd byte=118
                { 3447, 3 }, // op=0xd byte=119
                { 3448, 11 }, // op=0xd byte=120
                { 3449, 7 }, // op=0xd byte=121
                { 3450, 6 }, // op=0xd byte=122
                { 3451, 6 }, // op=0xd byte=123
                { 3452, 7 }, // op=0xd byte=124
                { 3453, 7 }, // op=0xd byte=125
                { 3454, 6 }, // op=0xd byte=126
                { 3455, 6 }, // op=0xd byte=127
                { 3456, 8 }, // op=0xd byte=128
                { 3457, 8 }, // op=0xd byte=129
                { 3458, 13 }, // op=0xd byte=130
                { 3459, 5 }, // op=0xd byte=131
                { 3460, 4 }, // op=0xd byte=132
                { 3461, 4 }, // op=0xd byte=133
                { 3462, 2 }, // op=0xd byte=134
                { 3463, 2 }, // op=0xd byte=135
                { 3464, 8 }, // op=0xd byte=136
                { 3465, 6 }, // op=0xd byte=137
                { 3466, 7 }, // op=0xd byte=138
                { 3467, 7 }, // op=0xd byte=139
                { 3468, 8 }, // op=0xd byte=140
                { 3469, 8 }, // op=0xd byte=141
                { 3470, 5 }, // op=0xd byte=142
                { 3471, 4 }, // op=0xd byte=143
                { 3472, 4 }, // op=0xd byte=144
                { 3473, 4 }, // op=0xd byte=145
                { 3474, 3 }, // op=0xd byte=146
                { 3475, 3 }, // op=0xd byte=147
                { 3476, 3 }, // op=0xd byte=148
                { 3477, 4 }, // op=0xd byte=149
                { 3478, 4 }, // op=0xd byte=150
                { 3479, 2 }, // op=0xd byte=151
                { 3480, 2 }, // op=0xd byte=152
                { 3481, 2 }, // op=0xd byte=153
                { 3482, 2 }, // op=0xd byte=154
                { 3483, 2 }, // op=0xd byte=155
                { 3484, 2 }, // op=0xd byte=156
                { 3485, 2 }, // op=0xd byte=157
                { 3486, 4 }, // op=0xd byte=158
                { 3487, 4 }, // op=0xd byte=159
                { 3488, 2 }, // op=0xd byte=160
                { 3489, 2 }, // op=0xd byte=161
                { 3492, 1 }, // op=0xd byte=164
                { 3493, 1 }, // op=0xd byte=165
                { 3494, 1 }, // op=0xd byte=166
                { 3495, 1 }, // op=0xd byte=167
                { 3496, 1 }, // op=0xd byte=168
                { 3497, 1 }, // op=0xd byte=169
                { 3498, 1 }, // op=0xd byte=170
                { 3499, 1 }, // op=0xd byte=171
                { 3500, 1 }, // op=0xd byte=172
                { 3501, 1 }, // op=0xd byte=173
                { 3502, 1 }, // op=0xd byte=174
                { 3503, 1 }, // op=0xd byte=175
                { 3584, 2188 }, // op=0xe byte=0
                { 3585, 34 }, // op=0xe byte=1
                { 3586, 33 }, // op=0xe byte=2
                { 3587, 56 }, // op=0xe byte=3
                { 3588, 2 }, // op=0xe byte=4
                { 3590, 22 }, // op=0xe byte=6
                { 3591, 53 }, // op=0xe byte=7
                { 3592, 30 }, // op=0xe byte=8
                { 3594, 13 }, // op=0xe byte=10
                { 3595, 68 }, // op=0xe byte=11
                { 3596, 32 }, // op=0xe byte=12
                { 3597, 7 }, // op=0xe byte=13
                { 3598, 20 }, // op=0xe byte=14
                { 3599, 1 }, // op=0xe byte=15
                { 3602, 1 }, // op=0xe byte=18
                { 3661, 1 }, // op=0xe byte=77
                { 3684, 31 }, // op=0xe byte=100
                { 3840, 6126 }, // op=0xf byte=0
                { 3841, 1033 }, // op=0xf byte=1
                { 3847, 1033 }, // op=0xf byte=7
                { 4095, 3376 }, // op=0xf byte=255
                { 4096, 4395 }, // op=0x10 byte=0
                { 4097, 525 }, // op=0x10 byte=1
                { 4098, 237 }, // op=0x10 byte=2
                { 4099, 192 }, // op=0x10 byte=3
                { 4100, 38 }, // op=0x10 byte=4
                { 4101, 216 }, // op=0x10 byte=5
                { 4102, 2 }, // op=0x10 byte=6
                { 4103, 9 }, // op=0x10 byte=7
                { 4118, 2 }, // op=0x10 byte=22
                { 4352, 30 }, // op=0x11 byte=0
                { 4353, 2 }, // op=0x11 byte=1
                { 4608, 16052 }, // op=0x12 byte=0
                { 4609, 1783 }, // op=0x12 byte=1
                { 4610, 487 }, // op=0x12 byte=2
                { 4611, 254 }, // op=0x12 byte=3
                { 4863, 40 }, // op=0x12 byte=255
                { 4864, 81 }, // op=0x13 byte=0
                { 4865, 4 }, // op=0x13 byte=1
                { 4866, 8 }, // op=0x13 byte=2
                { 4867, 4 }, // op=0x13 byte=3
                { 4868, 4 }, // op=0x13 byte=4
                { 4869, 3 }, // op=0x13 byte=5
                { 5120, 2305 }, // op=0x14 byte=0
                { 5121, 171 }, // op=0x14 byte=1
                { 5122, 19 }, // op=0x14 byte=2
                { 5123, 61 }, // op=0x14 byte=3
                { 5124, 15 }, // op=0x14 byte=4
                { 5125, 30 }, // op=0x14 byte=5
                { 5126, 7 }, // op=0x14 byte=6
                { 5127, 40 }, // op=0x14 byte=7
                { 5128, 39 }, // op=0x14 byte=8
                { 5129, 34 }, // op=0x14 byte=9
                { 5130, 9 }, // op=0x14 byte=10
                { 5131, 37 }, // op=0x14 byte=11
                { 5132, 12 }, // op=0x14 byte=12
                { 5133, 7 }, // op=0x14 byte=13
                { 5134, 14 }, // op=0x14 byte=14
                { 5376, 135 }, // op=0x15 byte=0
                { 5377, 31 }, // op=0x15 byte=1
                { 5378, 14 }, // op=0x15 byte=2
                { 5379, 1 }, // op=0x15 byte=3
                { 5380, 1 }, // op=0x15 byte=4
                { 5381, 1 }, // op=0x15 byte=5
                { 5382, 1 }, // op=0x15 byte=6
                { 5383, 1 }, // op=0x15 byte=7
                { 5384, 1 }, // op=0x15 byte=8
                { 5385, 1 }, // op=0x15 byte=9
                { 5386, 1 }, // op=0x15 byte=10
                { 5387, 1 }, // op=0x15 byte=11
                { 5420, 27 }, // op=0x15 byte=44
                { 5632, 6431 }, // op=0x16 byte=0
                { 5633, 122 }, // op=0x16 byte=1
                { 5634, 509 }, // op=0x16 byte=2
                { 5635, 2 }, // op=0x16 byte=3
                { 5888, 65850 }, // op=0x17 byte=0
                { 5889, 4815 }, // op=0x17 byte=1
                { 5890, 2234 }, // op=0x17 byte=2
                { 5891, 688 }, // op=0x17 byte=3
                { 5892, 443 }, // op=0x17 byte=4
                { 5893, 379 }, // op=0x17 byte=5
                { 5894, 347 }, // op=0x17 byte=6
                { 5895, 212 }, // op=0x17 byte=7
                { 5896, 249 }, // op=0x17 byte=8
                { 5897, 230 }, // op=0x17 byte=9
                { 5898, 218 }, // op=0x17 byte=10
                { 5899, 150 }, // op=0x17 byte=11
                { 5900, 219 }, // op=0x17 byte=12
                { 5901, 128 }, // op=0x17 byte=13
                { 5902, 176 }, // op=0x17 byte=14
                { 5903, 119 }, // op=0x17 byte=15
                { 5904, 1293 }, // op=0x17 byte=16
                { 5905, 85 }, // op=0x17 byte=17
                { 5906, 90 }, // op=0x17 byte=18
                { 5907, 53 }, // op=0x17 byte=19
                { 5908, 101 }, // op=0x17 byte=20
                { 5909, 100 }, // op=0x17 byte=21
                { 5910, 46 }, // op=0x17 byte=22
                { 5911, 43 }, // op=0x17 byte=23
                { 5912, 72 }, // op=0x17 byte=24
                { 5913, 74 }, // op=0x17 byte=25
                { 5914, 72 }, // op=0x17 byte=26
                { 5915, 33 }, // op=0x17 byte=27
                { 5916, 54 }, // op=0x17 byte=28
                { 5917, 34 }, // op=0x17 byte=29
                { 5918, 66 }, // op=0x17 byte=30
                { 5919, 54 }, // op=0x17 byte=31
                { 5920, 42 }, // op=0x17 byte=32
                { 5921, 52 }, // op=0x17 byte=33
                { 5922, 59 }, // op=0x17 byte=34
                { 5923, 23 }, // op=0x17 byte=35
                { 5924, 25 }, // op=0x17 byte=36
                { 5925, 12 }, // op=0x17 byte=37
                { 5926, 7 }, // op=0x17 byte=38
                { 5927, 39 }, // op=0x17 byte=39
                { 5928, 54 }, // op=0x17 byte=40
                { 5929, 72 }, // op=0x17 byte=41
                { 5930, 34 }, // op=0x17 byte=42
                { 5931, 44 }, // op=0x17 byte=43
                { 5932, 323 }, // op=0x17 byte=44
                { 5933, 31 }, // op=0x17 byte=45
                { 5934, 18 }, // op=0x17 byte=46
                { 5935, 6 }, // op=0x17 byte=47
                { 5936, 22 }, // op=0x17 byte=48
                { 5937, 7 }, // op=0x17 byte=49
                { 5938, 27 }, // op=0x17 byte=50
                { 5939, 32 }, // op=0x17 byte=51
                { 5940, 13 }, // op=0x17 byte=52
                { 5941, 30 }, // op=0x17 byte=53
                { 5942, 14 }, // op=0x17 byte=54
                { 5943, 108 }, // op=0x17 byte=55
                { 5944, 139 }, // op=0x17 byte=56
                { 5945, 120 }, // op=0x17 byte=57
                { 5946, 80 }, // op=0x17 byte=58
                { 5947, 11 }, // op=0x17 byte=59
                { 5948, 34 }, // op=0x17 byte=60
                { 5949, 45 }, // op=0x17 byte=61
                { 5950, 27 }, // op=0x17 byte=62
                { 5951, 25 }, // op=0x17 byte=63
                { 5952, 32 }, // op=0x17 byte=64
                { 5953, 9 }, // op=0x17 byte=65
                { 5954, 35 }, // op=0x17 byte=66
                { 5955, 12 }, // op=0x17 byte=67
                { 5956, 6 }, // op=0x17 byte=68
                { 5957, 11 }, // op=0x17 byte=69
                { 5958, 80 }, // op=0x17 byte=70
                { 5959, 43 }, // op=0x17 byte=71
                { 5960, 14 }, // op=0x17 byte=72
                { 5961, 5 }, // op=0x17 byte=73
                { 5962, 11 }, // op=0x17 byte=74
                { 5963, 18 }, // op=0x17 byte=75
                { 5964, 4 }, // op=0x17 byte=76
                { 5965, 26 }, // op=0x17 byte=77
                { 5966, 3 }, // op=0x17 byte=78
                { 5967, 40 }, // op=0x17 byte=79
                { 5968, 16 }, // op=0x17 byte=80
                { 5969, 25 }, // op=0x17 byte=81
                { 5970, 22 }, // op=0x17 byte=82
                { 5971, 9 }, // op=0x17 byte=83
                { 5972, 31 }, // op=0x17 byte=84
                { 5973, 28 }, // op=0x17 byte=85
                { 5974, 17 }, // op=0x17 byte=86
                { 5975, 29 }, // op=0x17 byte=87
                { 5976, 14 }, // op=0x17 byte=88
                { 5977, 37 }, // op=0x17 byte=89
                { 5978, 73 }, // op=0x17 byte=90
                { 5979, 16 }, // op=0x17 byte=91
                { 5980, 101 }, // op=0x17 byte=92
                { 5981, 69 }, // op=0x17 byte=93
                { 5982, 11 }, // op=0x17 byte=94
                { 5983, 11 }, // op=0x17 byte=95
                { 5984, 28 }, // op=0x17 byte=96
                { 5985, 16 }, // op=0x17 byte=97
                { 5986, 25 }, // op=0x17 byte=98
                { 5987, 9 }, // op=0x17 byte=99
                { 5988, 108 }, // op=0x17 byte=100
                { 5989, 32 }, // op=0x17 byte=101
                { 5990, 1 }, // op=0x17 byte=102
                { 5995, 3 }, // op=0x17 byte=107
                { 5996, 5 }, // op=0x17 byte=108
                { 5999, 19 }, // op=0x17 byte=111
                { 6000, 19 }, // op=0x17 byte=112
                { 6001, 72 }, // op=0x17 byte=113
                { 6006, 9 }, // op=0x17 byte=118
                { 6007, 15 }, // op=0x17 byte=119
                { 6008, 26 }, // op=0x17 byte=120
                { 6009, 21 }, // op=0x17 byte=121
                { 6010, 27 }, // op=0x17 byte=122
                { 6011, 26 }, // op=0x17 byte=123
                { 6012, 4 }, // op=0x17 byte=124
                { 6016, 16 }, // op=0x17 byte=128
                { 6018, 17 }, // op=0x17 byte=130
                { 6019, 5 }, // op=0x17 byte=131
                { 6020, 67 }, // op=0x17 byte=132
                { 6021, 22 }, // op=0x17 byte=133
                { 6022, 13 }, // op=0x17 byte=134
                { 6023, 26 }, // op=0x17 byte=135
                { 6024, 15 }, // op=0x17 byte=136
                { 6025, 26 }, // op=0x17 byte=137
                { 6026, 25 }, // op=0x17 byte=138
                { 6027, 12 }, // op=0x17 byte=139
                { 6028, 25 }, // op=0x17 byte=140
                { 6029, 37 }, // op=0x17 byte=141
                { 6030, 10 }, // op=0x17 byte=142
                { 6031, 18 }, // op=0x17 byte=143
                { 6032, 13 }, // op=0x17 byte=144
                { 6033, 16 }, // op=0x17 byte=145
                { 6034, 50 }, // op=0x17 byte=146
                { 6035, 46 }, // op=0x17 byte=147
                { 6036, 22 }, // op=0x17 byte=148
                { 6037, 13 }, // op=0x17 byte=149
                { 6038, 13 }, // op=0x17 byte=150
                { 6039, 13 }, // op=0x17 byte=151
                { 6040, 13 }, // op=0x17 byte=152
                { 6041, 13 }, // op=0x17 byte=153
                { 6042, 14 }, // op=0x17 byte=154
                { 6043, 12 }, // op=0x17 byte=155
                { 6044, 13 }, // op=0x17 byte=156
                { 6045, 15 }, // op=0x17 byte=157
                { 6046, 12 }, // op=0x17 byte=158
                { 6047, 26 }, // op=0x17 byte=159
                { 6048, 16 }, // op=0x17 byte=160
                { 6049, 15 }, // op=0x17 byte=161
                { 6050, 45 }, // op=0x17 byte=162
                { 6051, 35 }, // op=0x17 byte=163
                { 6052, 18 }, // op=0x17 byte=164
                { 6053, 17 }, // op=0x17 byte=165
                { 6054, 11 }, // op=0x17 byte=166
                { 6055, 40 }, // op=0x17 byte=167
                { 6056, 14 }, // op=0x17 byte=168
                { 6057, 24 }, // op=0x17 byte=169
                { 6059, 1 }, // op=0x17 byte=171
                { 6060, 85 }, // op=0x17 byte=172
                { 6064, 12 }, // op=0x17 byte=176
                { 6065, 16 }, // op=0x17 byte=177
                { 6066, 15 }, // op=0x17 byte=178
                { 6067, 14 }, // op=0x17 byte=179
                { 6068, 15 }, // op=0x17 byte=180
                { 6069, 13 }, // op=0x17 byte=181
                { 6070, 13 }, // op=0x17 byte=182
                { 6071, 7 }, // op=0x17 byte=183
                { 6072, 15 }, // op=0x17 byte=184
                { 6074, 21 }, // op=0x17 byte=186
                { 6075, 17 }, // op=0x17 byte=187
                { 6076, 64 }, // op=0x17 byte=188
                { 6078, 18 }, // op=0x17 byte=190
                { 6079, 19 }, // op=0x17 byte=191
                { 6080, 12 }, // op=0x17 byte=192
                { 6081, 27 }, // op=0x17 byte=193
                { 6082, 13 }, // op=0x17 byte=194
                { 6083, 69 }, // op=0x17 byte=195
                { 6088, 16 }, // op=0x17 byte=200
                { 6091, 9 }, // op=0x17 byte=203
                { 6092, 14 }, // op=0x17 byte=204
                { 6093, 12 }, // op=0x17 byte=205
                { 6095, 25 }, // op=0x17 byte=207
                { 6097, 11 }, // op=0x17 byte=209
                { 6098, 16 }, // op=0x17 byte=210
                { 6099, 11 }, // op=0x17 byte=211
                { 6100, 15 }, // op=0x17 byte=212
                { 6101, 21 }, // op=0x17 byte=213
                { 6102, 11 }, // op=0x17 byte=214
                { 6103, 7 }, // op=0x17 byte=215
                { 6104, 6 }, // op=0x17 byte=216
                { 6105, 3 }, // op=0x17 byte=217
                { 6106, 2 }, // op=0x17 byte=218
                { 6108, 2 }, // op=0x17 byte=220
                { 6109, 2 }, // op=0x17 byte=221
                { 6110, 2 }, // op=0x17 byte=222
                { 6111, 2 }, // op=0x17 byte=223
                { 6112, 2 }, // op=0x17 byte=224
                { 6113, 2 }, // op=0x17 byte=225
                { 6114, 2 }, // op=0x17 byte=226
                { 6117, 3 }, // op=0x17 byte=229
                { 6118, 9 }, // op=0x17 byte=230
                { 6119, 11 }, // op=0x17 byte=231
                { 6120, 2 }, // op=0x17 byte=232
                { 6121, 2 }, // op=0x17 byte=233
                { 6122, 2 }, // op=0x17 byte=234
                { 6123, 2 }, // op=0x17 byte=235
                { 6124, 1 }, // op=0x17 byte=236
                { 6125, 2 }, // op=0x17 byte=237
                { 6126, 2 }, // op=0x17 byte=238
                { 6127, 2 }, // op=0x17 byte=239
                { 6128, 1 }, // op=0x17 byte=240
                { 6129, 2 }, // op=0x17 byte=241
                { 6130, 2 }, // op=0x17 byte=242
                { 6131, 2 }, // op=0x17 byte=243
                { 6132, 2 }, // op=0x17 byte=244
                { 6133, 2 }, // op=0x17 byte=245
                { 6134, 1 }, // op=0x17 byte=246
                { 6135, 1 }, // op=0x17 byte=247
                { 6136, 2 }, // op=0x17 byte=248
                { 6137, 1 }, // op=0x17 byte=249
                { 6138, 1 }, // op=0x17 byte=250
                { 6140, 3 }, // op=0x17 byte=252
                { 6141, 1 }, // op=0x17 byte=253
                { 6143, 72 }, // op=0x17 byte=255
                { 6144, 2036 }, // op=0x18 byte=0
                { 6145, 584 }, // op=0x18 byte=1
                { 6146, 47 }, // op=0x18 byte=2
                { 6147, 101 }, // op=0x18 byte=3
                { 6400, 2835 }, // op=0x19 byte=0
                { 6401, 442 }, // op=0x19 byte=1
                { 6402, 307 }, // op=0x19 byte=2
                { 6403, 847 }, // op=0x19 byte=3
                { 6404, 15 }, // op=0x19 byte=4
                { 6405, 58 }, // op=0x19 byte=5
                { 6406, 12 }, // op=0x19 byte=6
                { 6407, 117 }, // op=0x19 byte=7
                { 6408, 68 }, // op=0x19 byte=8
                { 6409, 18 }, // op=0x19 byte=9
                { 6410, 3 }, // op=0x19 byte=10
                { 6411, 5 }, // op=0x19 byte=11
                { 6413, 1 }, // op=0x19 byte=13
                { 6415, 48 }, // op=0x19 byte=15
                { 6416, 2 }, // op=0x19 byte=16
                { 6417, 1 }, // op=0x19 byte=17
                { 6418, 1 }, // op=0x19 byte=18
                { 6419, 10 }, // op=0x19 byte=19
                { 6420, 1 }, // op=0x19 byte=20
                { 6423, 3 }, // op=0x19 byte=23
                { 6425, 153 }, // op=0x19 byte=25
                { 6426, 332 }, // op=0x19 byte=26
                { 6427, 4 }, // op=0x19 byte=27
                { 6428, 4 }, // op=0x19 byte=28
                { 6431, 1 }, // op=0x19 byte=31
                { 6432, 21 }, // op=0x19 byte=32
                { 6439, 1 }, // op=0x19 byte=39
                { 6441, 1 }, // op=0x19 byte=41
                { 6444, 2 }, // op=0x19 byte=44
                { 6445, 3 }, // op=0x19 byte=45
                { 6446, 2 }, // op=0x19 byte=46
                { 6448, 1 }, // op=0x19 byte=48
                { 6450, 5 }, // op=0x19 byte=50
                { 6464, 1 }, // op=0x19 byte=64
                { 6488, 2 }, // op=0x19 byte=88
                { 6500, 5 }, // op=0x19 byte=100
                { 6512, 3 }, // op=0x19 byte=112
                { 6517, 1 }, // op=0x19 byte=117
                { 6528, 39 }, // op=0x19 byte=128
                { 6530, 7 }, // op=0x19 byte=130
                { 6531, 23 }, // op=0x19 byte=131
                { 6532, 12 }, // op=0x19 byte=132
                { 6533, 4 }, // op=0x19 byte=133
                { 6534, 29 }, // op=0x19 byte=134
                { 6535, 2 }, // op=0x19 byte=135
                { 6536, 10 }, // op=0x19 byte=136
                { 6538, 4 }, // op=0x19 byte=138
                { 6539, 1 }, // op=0x19 byte=139
                { 6544, 2 }, // op=0x19 byte=144
                { 6560, 9 }, // op=0x19 byte=160
                { 6584, 4 }, // op=0x19 byte=184
                { 6600, 2 }, // op=0x19 byte=200
                { 6608, 13 }, // op=0x19 byte=208
                { 6632, 14 }, // op=0x19 byte=232
                { 6644, 6 }, // op=0x19 byte=244
                { 6650, 3 }, // op=0x19 byte=250
                { 6656, 13659 }, // op=0x1a byte=0
                { 6657, 3553 }, // op=0x1a byte=1
                { 6658, 1564 }, // op=0x1a byte=2
                { 6659, 674 }, // op=0x1a byte=3
                { 6660, 773 }, // op=0x1a byte=4
                { 6661, 56 }, // op=0x1a byte=5
                { 6662, 80 }, // op=0x1a byte=6
                { 6663, 358 }, // op=0x1a byte=7
                { 6664, 1076 }, // op=0x1a byte=8
                { 6665, 524 }, // op=0x1a byte=9
                { 6666, 4 }, // op=0x1a byte=10
                { 6667, 1 }, // op=0x1a byte=11
                { 6668, 2 }, // op=0x1a byte=12
                { 6670, 2 }, // op=0x1a byte=14
                { 6675, 1 }, // op=0x1a byte=19
                { 6681, 904 }, // op=0x1a byte=25
                { 6682, 39 }, // op=0x1a byte=26
                { 6689, 9 }, // op=0x1a byte=33
                { 6706, 4 }, // op=0x1a byte=50
                { 6711, 2 }, // op=0x1a byte=55
                { 6713, 1 }, // op=0x1a byte=57
                { 6721, 8 }, // op=0x1a byte=65
                { 6731, 7 }, // op=0x1a byte=75
                { 6746, 1 }, // op=0x1a byte=90
                { 6756, 8 }, // op=0x1a byte=100
                { 6784, 4 }, // op=0x1a byte=128
                { 6785, 4 }, // op=0x1a byte=129
                { 6786, 4 }, // op=0x1a byte=130
                { 6787, 5 }, // op=0x1a byte=131
                { 6788, 1 }, // op=0x1a byte=132
                { 6789, 1 }, // op=0x1a byte=133
                { 6790, 1 }, // op=0x1a byte=134
                { 6791, 10 }, // op=0x1a byte=135
                { 6792, 1 }, // op=0x1a byte=136
                { 6797, 2 }, // op=0x1a byte=141
                { 6806, 3 }, // op=0x1a byte=150
                { 6911, 6 }, // op=0x1a byte=255
                { 6912, 12154 }, // op=0x1b byte=0
                { 6913, 65 }, // op=0x1b byte=1
                { 6914, 430 }, // op=0x1b byte=2
                { 6915, 2 }, // op=0x1b byte=3
                { 6916, 503 }, // op=0x1b byte=4
                { 6917, 158 }, // op=0x1b byte=5
                { 6918, 493 }, // op=0x1b byte=6
                { 6919, 5 }, // op=0x1b byte=7
                { 6922, 41 }, // op=0x1b byte=10
                { 7166, 1864 }, // op=0x1b byte=254
                { 7167, 645 }, // op=0x1b byte=255
                { 7168, 9879 }, // op=0x1c byte=0
                { 7169, 4031 }, // op=0x1c byte=1
                { 7170, 1801 }, // op=0x1c byte=2
                { 7171, 186 }, // op=0x1c byte=3
                { 7172, 108 }, // op=0x1c byte=4
                { 7173, 171 }, // op=0x1c byte=5
                { 7174, 71 }, // op=0x1c byte=6
                { 7175, 638 }, // op=0x1c byte=7
                { 7176, 24 }, // op=0x1c byte=8
                { 7177, 26 }, // op=0x1c byte=9
                { 7178, 21 }, // op=0x1c byte=10
                { 7179, 11 }, // op=0x1c byte=11
                { 7180, 15 }, // op=0x1c byte=12
                { 7181, 17 }, // op=0x1c byte=13
                { 7182, 13 }, // op=0x1c byte=14
                { 7183, 12 }, // op=0x1c byte=15
                { 7184, 30 }, // op=0x1c byte=16
                { 7185, 17 }, // op=0x1c byte=17
                { 7186, 10 }, // op=0x1c byte=18
                { 7187, 8 }, // op=0x1c byte=19
                { 7188, 4 }, // op=0x1c byte=20
                { 7189, 3 }, // op=0x1c byte=21
                { 7190, 10 }, // op=0x1c byte=22
                { 7191, 12 }, // op=0x1c byte=23
                { 7192, 8 }, // op=0x1c byte=24
                { 7193, 7 }, // op=0x1c byte=25
                { 7194, 2 }, // op=0x1c byte=26
                { 7195, 5 }, // op=0x1c byte=27
                { 7196, 3 }, // op=0x1c byte=28
                { 7197, 2 }, // op=0x1c byte=29
                { 7198, 3 }, // op=0x1c byte=30
                { 7199, 2 }, // op=0x1c byte=31
                { 7200, 1 }, // op=0x1c byte=32
                { 7201, 9 }, // op=0x1c byte=33
                { 7202, 5 }, // op=0x1c byte=34
                { 7204, 4 }, // op=0x1c byte=36
                { 7205, 24 }, // op=0x1c byte=37
                { 7206, 9 }, // op=0x1c byte=38
                { 7207, 134 }, // op=0x1c byte=39
                { 7208, 4 }, // op=0x1c byte=40
                { 7209, 2 }, // op=0x1c byte=41
                { 7210, 2 }, // op=0x1c byte=42
                { 7211, 2 }, // op=0x1c byte=43
                { 7212, 2 }, // op=0x1c byte=44
                { 7213, 3 }, // op=0x1c byte=45
                { 7214, 3 }, // op=0x1c byte=46
                { 7215, 2497 }, // op=0x1c byte=47
                { 7217, 1 }, // op=0x1c byte=49
                { 7218, 3 }, // op=0x1c byte=50
                { 7219, 8 }, // op=0x1c byte=51
                { 7221, 1 }, // op=0x1c byte=53
                { 7222, 3 }, // op=0x1c byte=54
                { 7223, 1 }, // op=0x1c byte=55
                { 7224, 1 }, // op=0x1c byte=56
                { 7226, 1 }, // op=0x1c byte=58
                { 7227, 1 }, // op=0x1c byte=59
                { 7228, 1 }, // op=0x1c byte=60
                { 7229, 1 }, // op=0x1c byte=61
                { 7230, 1 }, // op=0x1c byte=62
                { 7231, 1 }, // op=0x1c byte=63
                { 7232, 1 }, // op=0x1c byte=64
                { 7233, 1 }, // op=0x1c byte=65
                { 7234, 1 }, // op=0x1c byte=66
                { 7235, 2 }, // op=0x1c byte=67
                { 7236, 2 }, // op=0x1c byte=68
                { 7237, 1 }, // op=0x1c byte=69
                { 7238, 1 }, // op=0x1c byte=70
                { 7239, 1 }, // op=0x1c byte=71
                { 7240, 2 }, // op=0x1c byte=72
                { 7241, 3 }, // op=0x1c byte=73
                { 7242, 5 }, // op=0x1c byte=74
                { 7243, 1 }, // op=0x1c byte=75
                { 7245, 1 }, // op=0x1c byte=77
                { 7246, 1 }, // op=0x1c byte=78
                { 7247, 3 }, // op=0x1c byte=79
                { 7248, 1 }, // op=0x1c byte=80
                { 7249, 4 }, // op=0x1c byte=81
                { 7250, 2 }, // op=0x1c byte=82
                { 7251, 1 }, // op=0x1c byte=83
                { 7252, 1 }, // op=0x1c byte=84
                { 7253, 2 }, // op=0x1c byte=85
                { 7254, 1 }, // op=0x1c byte=86
                { 7255, 1 }, // op=0x1c byte=87
                { 7256, 2 }, // op=0x1c byte=88
                { 7257, 1 }, // op=0x1c byte=89
                { 7258, 1 }, // op=0x1c byte=90
                { 7259, 1 }, // op=0x1c byte=91
                { 7260, 1 }, // op=0x1c byte=92
                { 7261, 1 }, // op=0x1c byte=93
                { 7262, 2 }, // op=0x1c byte=94
                { 7263, 2 }, // op=0x1c byte=95
                { 7264, 1 }, // op=0x1c byte=96
                { 7265, 1 }, // op=0x1c byte=97
                { 7266, 1 }, // op=0x1c byte=98
                { 7267, 1 }, // op=0x1c byte=99
                { 7268, 1 }, // op=0x1c byte=100
                { 7269, 1 }, // op=0x1c byte=101
                { 7271, 2 }, // op=0x1c byte=103
                { 7272, 2 }, // op=0x1c byte=104
                { 7273, 3 }, // op=0x1c byte=105
                { 7274, 1 }, // op=0x1c byte=106
                { 7275, 1 }, // op=0x1c byte=107
                { 7276, 1 }, // op=0x1c byte=108
                { 7277, 1 }, // op=0x1c byte=109
                { 7278, 1 }, // op=0x1c byte=110
                { 7279, 1 }, // op=0x1c byte=111
                { 7281, 1 }, // op=0x1c byte=113
                { 7283, 1 }, // op=0x1c byte=115
                { 7285, 1 }, // op=0x1c byte=117
                { 7287, 1 }, // op=0x1c byte=119
                { 7288, 1 }, // op=0x1c byte=120
                { 7289, 1 }, // op=0x1c byte=121
                { 7290, 1 }, // op=0x1c byte=122
                { 7291, 1 }, // op=0x1c byte=123
                { 7424, 3525 }, // op=0x1d byte=0
                { 7425, 1402 }, // op=0x1d byte=1
                { 7426, 440 }, // op=0x1d byte=2
                { 7427, 282 }, // op=0x1d byte=3
                { 7428, 81 }, // op=0x1d byte=4
                { 7429, 571 }, // op=0x1d byte=5
                { 7430, 317 }, // op=0x1d byte=6
                { 7431, 1092 }, // op=0x1d byte=7
                { 7432, 291 }, // op=0x1d byte=8
                { 7433, 134 }, // op=0x1d byte=9
                { 7434, 59 }, // op=0x1d byte=10
                { 7435, 44 }, // op=0x1d byte=11
                { 7436, 48 }, // op=0x1d byte=12
                { 7437, 150 }, // op=0x1d byte=13
                { 7438, 65 }, // op=0x1d byte=14
                { 7439, 729 }, // op=0x1d byte=15
                { 7440, 20 }, // op=0x1d byte=16
                { 7441, 4 }, // op=0x1d byte=17
                { 7442, 8 }, // op=0x1d byte=18
                { 7443, 5 }, // op=0x1d byte=19
                { 7444, 325 }, // op=0x1d byte=20
                { 7445, 4 }, // op=0x1d byte=21
                { 7446, 7 }, // op=0x1d byte=22
                { 7447, 15 }, // op=0x1d byte=23
                { 7448, 15 }, // op=0x1d byte=24
                { 7449, 90 }, // op=0x1d byte=25
                { 7450, 2297 }, // op=0x1d byte=26
                { 7451, 1 }, // op=0x1d byte=27
                { 7452, 1 }, // op=0x1d byte=28
                { 7453, 1 }, // op=0x1d byte=29
                { 7454, 22 }, // op=0x1d byte=30
                { 7455, 4 }, // op=0x1d byte=31
                { 7456, 23 }, // op=0x1d byte=32
                { 7457, 13 }, // op=0x1d byte=33
                { 7458, 44 }, // op=0x1d byte=34
                { 7459, 9 }, // op=0x1d byte=35
                { 7460, 3 }, // op=0x1d byte=36
                { 7461, 49 }, // op=0x1d byte=37
                { 7462, 1 }, // op=0x1d byte=38
                { 7463, 4 }, // op=0x1d byte=39
                { 7464, 33 }, // op=0x1d byte=40
                { 7465, 1 }, // op=0x1d byte=41
                { 7469, 2 }, // op=0x1d byte=45
                { 7474, 30 }, // op=0x1d byte=50
                { 7476, 1 }, // op=0x1d byte=52
                { 7477, 1 }, // op=0x1d byte=53
                { 7479, 1 }, // op=0x1d byte=55
                { 7483, 1 }, // op=0x1d byte=59
                { 7484, 1 }, // op=0x1d byte=60
                { 7485, 1 }, // op=0x1d byte=61
                { 7486, 1 }, // op=0x1d byte=62
                { 7489, 5 }, // op=0x1d byte=65
                { 7499, 6 }, // op=0x1d byte=75
                { 7504, 33 }, // op=0x1d byte=80
                { 7514, 1 }, // op=0x1d byte=90
                { 7524, 488 }, // op=0x1d byte=100
                { 7552, 111 }, // op=0x1d byte=128
                { 7553, 134 }, // op=0x1d byte=129
                { 7554, 67 }, // op=0x1d byte=130
                { 7555, 45 }, // op=0x1d byte=131
                { 7556, 38 }, // op=0x1d byte=132
                { 7557, 28 }, // op=0x1d byte=133
                { 7558, 27 }, // op=0x1d byte=134
                { 7559, 28 }, // op=0x1d byte=135
                { 7560, 81 }, // op=0x1d byte=136
                { 7561, 80 }, // op=0x1d byte=137
                { 7562, 21 }, // op=0x1d byte=138
                { 7563, 14 }, // op=0x1d byte=139
                { 7564, 11 }, // op=0x1d byte=140
                { 7565, 18 }, // op=0x1d byte=141
                { 7566, 11 }, // op=0x1d byte=142
                { 7567, 8 }, // op=0x1d byte=143
                { 7568, 23 }, // op=0x1d byte=144
                { 7569, 5 }, // op=0x1d byte=145
                { 7570, 6 }, // op=0x1d byte=146
                { 7571, 3 }, // op=0x1d byte=147
                { 7572, 2 }, // op=0x1d byte=148
                { 7576, 1 }, // op=0x1d byte=152
                { 7577, 1 }, // op=0x1d byte=153
                { 7578, 1 }, // op=0x1d byte=154
                { 7580, 17 }, // op=0x1d byte=156
                { 7582, 9 }, // op=0x1d byte=158
                { 7587, 1 }, // op=0x1d byte=163
                { 7590, 4 }, // op=0x1d byte=166
                { 7600, 6 }, // op=0x1d byte=176
                { 7605, 1 }, // op=0x1d byte=181
                { 7608, 3 }, // op=0x1d byte=184
                { 7624, 13 }, // op=0x1d byte=200
                { 7630, 7 }, // op=0x1d byte=206
                { 7632, 2 }, // op=0x1d byte=208
                { 7650, 2 }, // op=0x1d byte=226
                { 7656, 4 }, // op=0x1d byte=232
                { 7679, 54 }, // op=0x1d byte=255
                { 7680, 248 }, // op=0x1e byte=0
                { 7936, 36793 }, // op=0x1f byte=0
                { 7937, 413 }, // op=0x1f byte=1
                { 7938, 240 }, // op=0x1f byte=2
                { 7939, 433 }, // op=0x1f byte=3
                { 7940, 159 }, // op=0x1f byte=4
                { 7941, 67 }, // op=0x1f byte=5
                { 7942, 57 }, // op=0x1f byte=6
                { 7943, 60 }, // op=0x1f byte=7
                { 7944, 53 }, // op=0x1f byte=8
                { 7945, 25 }, // op=0x1f byte=9
                { 7946, 6772 }, // op=0x1f byte=10
                { 7947, 118 }, // op=0x1f byte=11
                { 7948, 43 }, // op=0x1f byte=12
                { 7949, 11 }, // op=0x1f byte=13
                { 7950, 10 }, // op=0x1f byte=14
                { 7951, 38 }, // op=0x1f byte=15
                { 7952, 17 }, // op=0x1f byte=16
                { 7953, 7 }, // op=0x1f byte=17
                { 7954, 137 }, // op=0x1f byte=18
                { 7955, 41 }, // op=0x1f byte=19
                { 7956, 41 }, // op=0x1f byte=20
                { 7957, 33 }, // op=0x1f byte=21
                { 7958, 21 }, // op=0x1f byte=22
                { 7959, 65 }, // op=0x1f byte=23
                { 7960, 47 }, // op=0x1f byte=24
                { 7961, 212 }, // op=0x1f byte=25
                { 7962, 32 }, // op=0x1f byte=26
                { 7963, 49 }, // op=0x1f byte=27
                { 7964, 17 }, // op=0x1f byte=28
                { 7965, 23 }, // op=0x1f byte=29
                { 7966, 21 }, // op=0x1f byte=30
                { 7967, 60 }, // op=0x1f byte=31
                { 7968, 85 }, // op=0x1f byte=32
                { 7969, 9 }, // op=0x1f byte=33
                { 7970, 7 }, // op=0x1f byte=34
                { 7971, 35 }, // op=0x1f byte=35
                { 7972, 52 }, // op=0x1f byte=36
                { 7973, 60 }, // op=0x1f byte=37
                { 7974, 20 }, // op=0x1f byte=38
                { 7975, 25 }, // op=0x1f byte=39
                { 7976, 23 }, // op=0x1f byte=40
                { 7977, 19 }, // op=0x1f byte=41
                { 7978, 15 }, // op=0x1f byte=42
                { 7979, 38 }, // op=0x1f byte=43
                { 7980, 9 }, // op=0x1f byte=44
                { 7981, 49 }, // op=0x1f byte=45
                { 7982, 10 }, // op=0x1f byte=46
                { 7983, 32 }, // op=0x1f byte=47
                { 7984, 9 }, // op=0x1f byte=48
                { 7985, 20 }, // op=0x1f byte=49
                { 7986, 10 }, // op=0x1f byte=50
                { 7987, 64 }, // op=0x1f byte=51
                { 7988, 47 }, // op=0x1f byte=52
                { 7989, 32 }, // op=0x1f byte=53
                { 7990, 34 }, // op=0x1f byte=54
                { 7991, 37 }, // op=0x1f byte=55
                { 7992, 28 }, // op=0x1f byte=56
                { 7993, 51 }, // op=0x1f byte=57
                { 7994, 20 }, // op=0x1f byte=58
                { 7995, 36 }, // op=0x1f byte=59
                { 7996, 24 }, // op=0x1f byte=60
                { 7997, 31 }, // op=0x1f byte=61
                { 7998, 64 }, // op=0x1f byte=62
                { 7999, 21 }, // op=0x1f byte=63
                { 8000, 39 }, // op=0x1f byte=64
                { 8001, 9 }, // op=0x1f byte=65
                { 8002, 48 }, // op=0x1f byte=66
                { 8003, 13 }, // op=0x1f byte=67
                { 8004, 28 }, // op=0x1f byte=68
                { 8005, 12 }, // op=0x1f byte=69
                { 8006, 8 }, // op=0x1f byte=70
                { 8007, 37 }, // op=0x1f byte=71
                { 8008, 17 }, // op=0x1f byte=72
                { 8009, 52 }, // op=0x1f byte=73
                { 8010, 104 }, // op=0x1f byte=74
                { 8011, 87 }, // op=0x1f byte=75
                { 8012, 15 }, // op=0x1f byte=76
                { 8013, 53 }, // op=0x1f byte=77
                { 8014, 7 }, // op=0x1f byte=78
                { 8015, 66 }, // op=0x1f byte=79
                { 8016, 31 }, // op=0x1f byte=80
                { 8017, 19 }, // op=0x1f byte=81
                { 8018, 43 }, // op=0x1f byte=82
                { 8019, 115 }, // op=0x1f byte=83
                { 8020, 52 }, // op=0x1f byte=84
                { 8021, 7 }, // op=0x1f byte=85
                { 8022, 94 }, // op=0x1f byte=86
                { 8023, 48 }, // op=0x1f byte=87
                { 8024, 28 }, // op=0x1f byte=88
                { 8025, 65 }, // op=0x1f byte=89
                { 8026, 162 }, // op=0x1f byte=90
                { 8027, 7 }, // op=0x1f byte=91
                { 8028, 40 }, // op=0x1f byte=92
                { 8029, 22 }, // op=0x1f byte=93
                { 8030, 9 }, // op=0x1f byte=94
                { 8031, 36 }, // op=0x1f byte=95
                { 8032, 23 }, // op=0x1f byte=96
                { 8033, 20 }, // op=0x1f byte=97
                { 8034, 33 }, // op=0x1f byte=98
                { 8035, 166 }, // op=0x1f byte=99
                { 8036, 12 }, // op=0x1f byte=100
                { 8037, 32 }, // op=0x1f byte=101
                { 8038, 21 }, // op=0x1f byte=102
                { 8039, 81 }, // op=0x1f byte=103
                { 8040, 86 }, // op=0x1f byte=104
                { 8041, 22 }, // op=0x1f byte=105
                { 8042, 50 }, // op=0x1f byte=106
                { 8043, 17 }, // op=0x1f byte=107
                { 8044, 30 }, // op=0x1f byte=108
                { 8045, 17 }, // op=0x1f byte=109
                { 8046, 36 }, // op=0x1f byte=110
                { 8047, 25 }, // op=0x1f byte=111
                { 8048, 81 }, // op=0x1f byte=112
                { 8049, 69 }, // op=0x1f byte=113
                { 8050, 22 }, // op=0x1f byte=114
                { 8051, 45 }, // op=0x1f byte=115
                { 8052, 89 }, // op=0x1f byte=116
                { 8053, 59 }, // op=0x1f byte=117
                { 8054, 29 }, // op=0x1f byte=118
                { 8055, 31 }, // op=0x1f byte=119
                { 8056, 18 }, // op=0x1f byte=120
                { 8057, 21 }, // op=0x1f byte=121
                { 8058, 15 }, // op=0x1f byte=122
                { 8059, 36 }, // op=0x1f byte=123
                { 8060, 7 }, // op=0x1f byte=124
                { 8061, 19 }, // op=0x1f byte=125
                { 8062, 31 }, // op=0x1f byte=126
                { 8063, 23 }, // op=0x1f byte=127
                { 8064, 466 }, // op=0x1f byte=128
                { 8065, 870 }, // op=0x1f byte=129
                { 8066, 101 }, // op=0x1f byte=130
                { 8067, 2498 }, // op=0x1f byte=131
                { 8068, 2172 }, // op=0x1f byte=132
                { 8069, 197 }, // op=0x1f byte=133
                { 8070, 364 }, // op=0x1f byte=134
                { 8071, 112 }, // op=0x1f byte=135
                { 8072, 316 }, // op=0x1f byte=136
                { 8073, 344 }, // op=0x1f byte=137
                { 8074, 32 }, // op=0x1f byte=138
                { 8075, 49 }, // op=0x1f byte=139
                { 8076, 73 }, // op=0x1f byte=140
                { 8077, 25 }, // op=0x1f byte=141
                { 8078, 38 }, // op=0x1f byte=142
                { 8079, 36 }, // op=0x1f byte=143
                { 8080, 83 }, // op=0x1f byte=144
                { 8081, 29 }, // op=0x1f byte=145
                { 8082, 51 }, // op=0x1f byte=146
                { 8083, 21 }, // op=0x1f byte=147
                { 8084, 19 }, // op=0x1f byte=148
                { 8085, 21 }, // op=0x1f byte=149
                { 8086, 61 }, // op=0x1f byte=150
                { 8087, 20 }, // op=0x1f byte=151
                { 8088, 11 }, // op=0x1f byte=152
                { 8089, 60 }, // op=0x1f byte=153
                { 8090, 48 }, // op=0x1f byte=154
                { 8091, 42 }, // op=0x1f byte=155
                { 8092, 88 }, // op=0x1f byte=156
                { 8093, 47 }, // op=0x1f byte=157
                { 8094, 22 }, // op=0x1f byte=158
                { 8095, 27 }, // op=0x1f byte=159
                { 8096, 11 }, // op=0x1f byte=160
                { 8097, 10 }, // op=0x1f byte=161
                { 8098, 45 }, // op=0x1f byte=162
                { 8099, 52 }, // op=0x1f byte=163
                { 8100, 50 }, // op=0x1f byte=164
                { 8101, 25 }, // op=0x1f byte=165
                { 8102, 42 }, // op=0x1f byte=166
                { 8103, 61 }, // op=0x1f byte=167
                { 8104, 7 }, // op=0x1f byte=168
                { 8105, 62 }, // op=0x1f byte=169
                { 8106, 17 }, // op=0x1f byte=170
                { 8107, 48 }, // op=0x1f byte=171
                { 8108, 16 }, // op=0x1f byte=172
                { 8109, 20 }, // op=0x1f byte=173
                { 8110, 41 }, // op=0x1f byte=174
                { 8111, 40 }, // op=0x1f byte=175
                { 8112, 28 }, // op=0x1f byte=176
                { 8113, 104 }, // op=0x1f byte=177
                { 8114, 47 }, // op=0x1f byte=178
                { 8115, 48 }, // op=0x1f byte=179
                { 8116, 45 }, // op=0x1f byte=180
                { 8117, 23 }, // op=0x1f byte=181
                { 8118, 21 }, // op=0x1f byte=182
                { 8119, 16 }, // op=0x1f byte=183
                { 8120, 46 }, // op=0x1f byte=184
                { 8121, 23 }, // op=0x1f byte=185
                { 8122, 77 }, // op=0x1f byte=186
                { 8123, 23 }, // op=0x1f byte=187
                { 8124, 41 }, // op=0x1f byte=188
                { 8125, 8 }, // op=0x1f byte=189
                { 8126, 125 }, // op=0x1f byte=190
                { 8127, 37 }, // op=0x1f byte=191
                { 8128, 62 }, // op=0x1f byte=192
                { 8129, 177 }, // op=0x1f byte=193
                { 8130, 25 }, // op=0x1f byte=194
                { 8131, 27 }, // op=0x1f byte=195
                { 8132, 19 }, // op=0x1f byte=196
                { 8133, 70 }, // op=0x1f byte=197
                { 8134, 70 }, // op=0x1f byte=198
                { 8135, 38 }, // op=0x1f byte=199
                { 8136, 25 }, // op=0x1f byte=200
                { 8137, 87 }, // op=0x1f byte=201
                { 8138, 14 }, // op=0x1f byte=202
                { 8139, 29 }, // op=0x1f byte=203
                { 8140, 36 }, // op=0x1f byte=204
                { 8141, 39 }, // op=0x1f byte=205
                { 8142, 13 }, // op=0x1f byte=206
                { 8143, 54 }, // op=0x1f byte=207
                { 8144, 37 }, // op=0x1f byte=208
                { 8145, 13 }, // op=0x1f byte=209
                { 8146, 14 }, // op=0x1f byte=210
                { 8147, 5 }, // op=0x1f byte=211
                { 8148, 22 }, // op=0x1f byte=212
                { 8149, 13 }, // op=0x1f byte=213
                { 8150, 32 }, // op=0x1f byte=214
                { 8151, 145 }, // op=0x1f byte=215
                { 8152, 61 }, // op=0x1f byte=216
                { 8153, 24 }, // op=0x1f byte=217
                { 8154, 4 }, // op=0x1f byte=218
                { 8155, 7 }, // op=0x1f byte=219
                { 8156, 34 }, // op=0x1f byte=220
                { 8157, 19 }, // op=0x1f byte=221
                { 8158, 15 }, // op=0x1f byte=222
                { 8159, 52 }, // op=0x1f byte=223
                { 8160, 47 }, // op=0x1f byte=224
                { 8161, 30 }, // op=0x1f byte=225
                { 8162, 13 }, // op=0x1f byte=226
                { 8163, 42 }, // op=0x1f byte=227
                { 8164, 35 }, // op=0x1f byte=228
                { 8165, 11 }, // op=0x1f byte=229
                { 8166, 130 }, // op=0x1f byte=230
                { 8167, 23 }, // op=0x1f byte=231
                { 8168, 11 }, // op=0x1f byte=232
                { 8169, 14 }, // op=0x1f byte=233
                { 8170, 83 }, // op=0x1f byte=234
                { 8171, 6 }, // op=0x1f byte=235
                { 8172, 27 }, // op=0x1f byte=236
                { 8173, 6 }, // op=0x1f byte=237
                { 8174, 32 }, // op=0x1f byte=238
                { 8175, 63 }, // op=0x1f byte=239
                { 8176, 7 }, // op=0x1f byte=240
                { 8177, 29 }, // op=0x1f byte=241
                { 8178, 11 }, // op=0x1f byte=242
                { 8179, 64 }, // op=0x1f byte=243
                { 8180, 54 }, // op=0x1f byte=244
                { 8181, 22 }, // op=0x1f byte=245
                { 8182, 80 }, // op=0x1f byte=246
                { 8183, 51 }, // op=0x1f byte=247
                { 8184, 40 }, // op=0x1f byte=248
                { 8185, 160 }, // op=0x1f byte=249
                { 8186, 8 }, // op=0x1f byte=250
                { 8187, 33 }, // op=0x1f byte=251
                { 8188, 31 }, // op=0x1f byte=252
                { 8189, 101 }, // op=0x1f byte=253
                { 8190, 19 }, // op=0x1f byte=254
                { 8191, 39 }, // op=0x1f byte=255
                { 8192, 18691 }, // op=0x20 byte=0
                { 8193, 132 }, // op=0x20 byte=1
                { 8194, 82 }, // op=0x20 byte=2
                { 8195, 1484 }, // op=0x20 byte=3
                { 8196, 95 }, // op=0x20 byte=4
                { 8197, 120 }, // op=0x20 byte=5
                { 8198, 43 }, // op=0x20 byte=6
                { 8199, 84 }, // op=0x20 byte=7
                { 8200, 64 }, // op=0x20 byte=8
                { 8201, 78 }, // op=0x20 byte=9
                { 8202, 5211 }, // op=0x20 byte=10
                { 8203, 1534 }, // op=0x20 byte=11
                { 8204, 114 }, // op=0x20 byte=12
                { 8205, 174 }, // op=0x20 byte=13
                { 8206, 88 }, // op=0x20 byte=14
                { 8207, 86 }, // op=0x20 byte=15
                { 8208, 79 }, // op=0x20 byte=16
                { 8209, 118 }, // op=0x20 byte=17
                { 8210, 87 }, // op=0x20 byte=18
                { 8211, 78 }, // op=0x20 byte=19
                { 8212, 169 }, // op=0x20 byte=20
                { 8213, 158 }, // op=0x20 byte=21
                { 8214, 91 }, // op=0x20 byte=22
                { 8215, 140 }, // op=0x20 byte=23
                { 8216, 54 }, // op=0x20 byte=24
                { 8217, 159 }, // op=0x20 byte=25
                { 8218, 76 }, // op=0x20 byte=26
                { 8219, 74 }, // op=0x20 byte=27
                { 8220, 52 }, // op=0x20 byte=28
                { 8221, 131 }, // op=0x20 byte=29
                { 8222, 114 }, // op=0x20 byte=30
                { 8223, 104 }, // op=0x20 byte=31
                { 8224, 86 }, // op=0x20 byte=32
                { 8225, 96 }, // op=0x20 byte=33
                { 8226, 96 }, // op=0x20 byte=34
                { 8227, 117 }, // op=0x20 byte=35
                { 8228, 91 }, // op=0x20 byte=36
                { 8229, 96 }, // op=0x20 byte=37
                { 8230, 93 }, // op=0x20 byte=38
                { 8231, 73 }, // op=0x20 byte=39
                { 8232, 40 }, // op=0x20 byte=40
                { 8233, 70 }, // op=0x20 byte=41
                { 8234, 126 }, // op=0x20 byte=42
                { 8235, 196 }, // op=0x20 byte=43
                { 8236, 79 }, // op=0x20 byte=44
                { 8237, 107 }, // op=0x20 byte=45
                { 8238, 95 }, // op=0x20 byte=46
                { 8239, 77 }, // op=0x20 byte=47
                { 8240, 72 }, // op=0x20 byte=48
                { 8241, 136 }, // op=0x20 byte=49
                { 8242, 48 }, // op=0x20 byte=50
                { 8243, 181 }, // op=0x20 byte=51
                { 8244, 185 }, // op=0x20 byte=52
                { 8245, 187 }, // op=0x20 byte=53
                { 8246, 78 }, // op=0x20 byte=54
                { 8247, 154 }, // op=0x20 byte=55
                { 8248, 83 }, // op=0x20 byte=56
                { 8249, 76 }, // op=0x20 byte=57
                { 8250, 55 }, // op=0x20 byte=58
                { 8251, 124 }, // op=0x20 byte=59
                { 8252, 84 }, // op=0x20 byte=60
                { 8253, 129 }, // op=0x20 byte=61
                { 8254, 104 }, // op=0x20 byte=62
                { 8255, 60 }, // op=0x20 byte=63
                { 8256, 86 }, // op=0x20 byte=64
                { 8257, 59 }, // op=0x20 byte=65
                { 8258, 126 }, // op=0x20 byte=66
                { 8259, 190 }, // op=0x20 byte=67
                { 8260, 101 }, // op=0x20 byte=68
                { 8261, 150 }, // op=0x20 byte=69
                { 8262, 97 }, // op=0x20 byte=70
                { 8263, 79 }, // op=0x20 byte=71
                { 8264, 81 }, // op=0x20 byte=72
                { 8265, 57 }, // op=0x20 byte=73
                { 8266, 163 }, // op=0x20 byte=74
                { 8267, 79 }, // op=0x20 byte=75
                { 8268, 133 }, // op=0x20 byte=76
                { 8269, 176 }, // op=0x20 byte=77
                { 8270, 88 }, // op=0x20 byte=78
                { 8271, 133 }, // op=0x20 byte=79
                { 8272, 192 }, // op=0x20 byte=80
                { 8273, 98 }, // op=0x20 byte=81
                { 8274, 130 }, // op=0x20 byte=82
                { 8275, 176 }, // op=0x20 byte=83
                { 8276, 115 }, // op=0x20 byte=84
                { 8277, 110 }, // op=0x20 byte=85
                { 8278, 100 }, // op=0x20 byte=86
                { 8279, 64 }, // op=0x20 byte=87
                { 8280, 132 }, // op=0x20 byte=88
                { 8281, 137 }, // op=0x20 byte=89
                { 8282, 130 }, // op=0x20 byte=90
                { 8283, 136 }, // op=0x20 byte=91
                { 8284, 156 }, // op=0x20 byte=92
                { 8285, 88 }, // op=0x20 byte=93
                { 8286, 86 }, // op=0x20 byte=94
                { 8287, 79 }, // op=0x20 byte=95
                { 8288, 71 }, // op=0x20 byte=96
                { 8289, 80 }, // op=0x20 byte=97
                { 8290, 152 }, // op=0x20 byte=98
                { 8291, 59 }, // op=0x20 byte=99
                { 8292, 190 }, // op=0x20 byte=100
                { 8293, 130 }, // op=0x20 byte=101
                { 8294, 182 }, // op=0x20 byte=102
                { 8295, 126 }, // op=0x20 byte=103
                { 8296, 81 }, // op=0x20 byte=104
                { 8297, 64 }, // op=0x20 byte=105
                { 8298, 97 }, // op=0x20 byte=106
                { 8299, 91 }, // op=0x20 byte=107
                { 8300, 108 }, // op=0x20 byte=108
                { 8301, 110 }, // op=0x20 byte=109
                { 8302, 70 }, // op=0x20 byte=110
                { 8303, 100 }, // op=0x20 byte=111
                { 8304, 72 }, // op=0x20 byte=112
                { 8305, 160 }, // op=0x20 byte=113
                { 8306, 113 }, // op=0x20 byte=114
                { 8307, 145 }, // op=0x20 byte=115
                { 8308, 76 }, // op=0x20 byte=116
                { 8309, 139 }, // op=0x20 byte=117
                { 8310, 80 }, // op=0x20 byte=118
                { 8311, 107 }, // op=0x20 byte=119
                { 8312, 130 }, // op=0x20 byte=120
                { 8313, 53 }, // op=0x20 byte=121
                { 8314, 111 }, // op=0x20 byte=122
                { 8315, 71 }, // op=0x20 byte=123
                { 8316, 83 }, // op=0x20 byte=124
                { 8317, 46 }, // op=0x20 byte=125
                { 8318, 86 }, // op=0x20 byte=126
                { 8319, 116 }, // op=0x20 byte=127
                { 8320, 146 }, // op=0x20 byte=128
                { 8321, 88 }, // op=0x20 byte=129
                { 8322, 169 }, // op=0x20 byte=130
                { 8323, 57 }, // op=0x20 byte=131
                { 8324, 104 }, // op=0x20 byte=132
                { 8325, 123 }, // op=0x20 byte=133
                { 8326, 111 }, // op=0x20 byte=134
                { 8327, 59 }, // op=0x20 byte=135
                { 8328, 110 }, // op=0x20 byte=136
                { 8329, 150 }, // op=0x20 byte=137
                { 8330, 66 }, // op=0x20 byte=138
                { 8331, 132 }, // op=0x20 byte=139
                { 8332, 88 }, // op=0x20 byte=140
                { 8333, 81 }, // op=0x20 byte=141
                { 8334, 103 }, // op=0x20 byte=142
                { 8335, 85 }, // op=0x20 byte=143
                { 8336, 97 }, // op=0x20 byte=144
                { 8337, 98 }, // op=0x20 byte=145
                { 8338, 112 }, // op=0x20 byte=146
                { 8339, 80 }, // op=0x20 byte=147
                { 8340, 189 }, // op=0x20 byte=148
                { 8341, 161 }, // op=0x20 byte=149
                { 8342, 167 }, // op=0x20 byte=150
                { 8343, 66 }, // op=0x20 byte=151
                { 8344, 109 }, // op=0x20 byte=152
                { 8345, 99 }, // op=0x20 byte=153
                { 8346, 131 }, // op=0x20 byte=154
                { 8347, 121 }, // op=0x20 byte=155
                { 8348, 57 }, // op=0x20 byte=156
                { 8349, 41 }, // op=0x20 byte=157
                { 8350, 106 }, // op=0x20 byte=158
                { 8351, 66 }, // op=0x20 byte=159
                { 8352, 88 }, // op=0x20 byte=160
                { 8353, 63 }, // op=0x20 byte=161
                { 8354, 91 }, // op=0x20 byte=162
                { 8355, 87 }, // op=0x20 byte=163
                { 8356, 72 }, // op=0x20 byte=164
                { 8357, 61 }, // op=0x20 byte=165
                { 8358, 85 }, // op=0x20 byte=166
                { 8359, 106 }, // op=0x20 byte=167
                { 8360, 80 }, // op=0x20 byte=168
                { 8361, 108 }, // op=0x20 byte=169
                { 8362, 57 }, // op=0x20 byte=170
                { 8363, 49 }, // op=0x20 byte=171
                { 8364, 109 }, // op=0x20 byte=172
                { 8365, 172 }, // op=0x20 byte=173
                { 8366, 67 }, // op=0x20 byte=174
                { 8367, 105 }, // op=0x20 byte=175
                { 8368, 75 }, // op=0x20 byte=176
                { 8369, 84 }, // op=0x20 byte=177
                { 8370, 56 }, // op=0x20 byte=178
                { 8371, 144 }, // op=0x20 byte=179
                { 8372, 105 }, // op=0x20 byte=180
                { 8373, 146 }, // op=0x20 byte=181
                { 8374, 79 }, // op=0x20 byte=182
                { 8375, 86 }, // op=0x20 byte=183
                { 8376, 95 }, // op=0x20 byte=184
                { 8377, 198 }, // op=0x20 byte=185
                { 8378, 102 }, // op=0x20 byte=186
                { 8379, 114 }, // op=0x20 byte=187
                { 8380, 113 }, // op=0x20 byte=188
                { 8381, 50 }, // op=0x20 byte=189
                { 8382, 78 }, // op=0x20 byte=190
                { 8383, 138 }, // op=0x20 byte=191
                { 8384, 109 }, // op=0x20 byte=192
                { 8385, 183 }, // op=0x20 byte=193
                { 8386, 127 }, // op=0x20 byte=194
                { 8387, 94 }, // op=0x20 byte=195
                { 8388, 115 }, // op=0x20 byte=196
                { 8389, 147 }, // op=0x20 byte=197
                { 8390, 97 }, // op=0x20 byte=198
                { 8391, 83 }, // op=0x20 byte=199
                { 8392, 65 }, // op=0x20 byte=200
                { 8393, 123 }, // op=0x20 byte=201
                { 8394, 80 }, // op=0x20 byte=202
                { 8395, 111 }, // op=0x20 byte=203
                { 8396, 80 }, // op=0x20 byte=204
                { 8397, 76 }, // op=0x20 byte=205
                { 8398, 70 }, // op=0x20 byte=206
                { 8399, 75 }, // op=0x20 byte=207
                { 8400, 35 }, // op=0x20 byte=208
                { 8401, 136 }, // op=0x20 byte=209
                { 8402, 111 }, // op=0x20 byte=210
                { 8403, 66 }, // op=0x20 byte=211
                { 8404, 132 }, // op=0x20 byte=212
                { 8405, 92 }, // op=0x20 byte=213
                { 8406, 65 }, // op=0x20 byte=214
                { 8407, 46 }, // op=0x20 byte=215
                { 8408, 140 }, // op=0x20 byte=216
                { 8409, 128 }, // op=0x20 byte=217
                { 8410, 114 }, // op=0x20 byte=218
                { 8411, 78 }, // op=0x20 byte=219
                { 8412, 143 }, // op=0x20 byte=220
                { 8413, 101 }, // op=0x20 byte=221
                { 8414, 52 }, // op=0x20 byte=222
                { 8415, 117 }, // op=0x20 byte=223
                { 8416, 142 }, // op=0x20 byte=224
                { 8417, 142 }, // op=0x20 byte=225
                { 8418, 65 }, // op=0x20 byte=226
                { 8419, 163 }, // op=0x20 byte=227
                { 8420, 88 }, // op=0x20 byte=228
                { 8421, 69 }, // op=0x20 byte=229
                { 8422, 57 }, // op=0x20 byte=230
                { 8423, 53 }, // op=0x20 byte=231
                { 8424, 130 }, // op=0x20 byte=232
                { 8425, 65 }, // op=0x20 byte=233
                { 8426, 208 }, // op=0x20 byte=234
                { 8427, 77 }, // op=0x20 byte=235
                { 8428, 177 }, // op=0x20 byte=236
                { 8429, 80 }, // op=0x20 byte=237
                { 8430, 107 }, // op=0x20 byte=238
                { 8431, 78 }, // op=0x20 byte=239
                { 8432, 31 }, // op=0x20 byte=240
                { 8433, 99 }, // op=0x20 byte=241
                { 8434, 86 }, // op=0x20 byte=242
                { 8435, 158 }, // op=0x20 byte=243
                { 8436, 116 }, // op=0x20 byte=244
                { 8437, 54 }, // op=0x20 byte=245
                { 8438, 148 }, // op=0x20 byte=246
                { 8439, 112 }, // op=0x20 byte=247
                { 8440, 85 }, // op=0x20 byte=248
                { 8441, 61 }, // op=0x20 byte=249
                { 8442, 45 }, // op=0x20 byte=250
                { 8443, 153 }, // op=0x20 byte=251
                { 8444, 220 }, // op=0x20 byte=252
                { 8445, 187 }, // op=0x20 byte=253
                { 8446, 185 }, // op=0x20 byte=254
                { 8447, 177 }, // op=0x20 byte=255
                { 8448, 16 }, // op=0x21 byte=0
                { 8704, 372 }, // op=0x22 byte=0
                { 8705, 32 }, // op=0x22 byte=1
                { 8706, 2 }, // op=0x22 byte=2
                { 8710, 2 }, // op=0x22 byte=6
                { 8714, 4 }, // op=0x22 byte=10
                { 8729, 52 }, // op=0x22 byte=25
                { 8960, 1466 }, // op=0x23 byte=0
                { 8961, 429 }, // op=0x23 byte=1
                { 8962, 398 }, // op=0x23 byte=2
                { 8963, 78 }, // op=0x23 byte=3
                { 8964, 17 }, // op=0x23 byte=4
                { 8965, 20 }, // op=0x23 byte=5
                { 8966, 2 }, // op=0x23 byte=6
                { 8967, 26 }, // op=0x23 byte=7
                { 8968, 7 }, // op=0x23 byte=8
                { 8969, 40 }, // op=0x23 byte=9
                { 8970, 294 }, // op=0x23 byte=10
                { 8971, 2 }, // op=0x23 byte=11
                { 8973, 12 }, // op=0x23 byte=13
                { 8975, 5 }, // op=0x23 byte=15
                { 8976, 2 }, // op=0x23 byte=16
                { 8977, 2 }, // op=0x23 byte=17
                { 8980, 4 }, // op=0x23 byte=20
                { 8981, 1 }, // op=0x23 byte=21
                { 8983, 8 }, // op=0x23 byte=23
                { 8985, 22 }, // op=0x23 byte=25
                { 8990, 3 }, // op=0x23 byte=30
                { 8999, 2 }, // op=0x23 byte=39
                { 9024, 3 }, // op=0x23 byte=64
                { 9025, 4 }, // op=0x23 byte=65
                { 9026, 2 }, // op=0x23 byte=66
                { 9027, 71 }, // op=0x23 byte=67
                { 9031, 12 }, // op=0x23 byte=71
                { 9033, 73 }, // op=0x23 byte=73
                { 9041, 1 }, // op=0x23 byte=81
                { 9043, 1 }, // op=0x23 byte=83
                { 9047, 1 }, // op=0x23 byte=87
                { 9049, 48 }, // op=0x23 byte=89
                { 9060, 6 }, // op=0x23 byte=100
                { 9215, 2 }, // op=0x23 byte=255
                { 9216, 14541 }, // op=0x24 byte=0
                { 9217, 1057 }, // op=0x24 byte=1
                { 9218, 850 }, // op=0x24 byte=2
                { 9219, 270 }, // op=0x24 byte=3
                { 9220, 534 }, // op=0x24 byte=4
                { 9221, 222 }, // op=0x24 byte=5
                { 9222, 540 }, // op=0x24 byte=6
                { 9223, 127 }, // op=0x24 byte=7
                { 9224, 597 }, // op=0x24 byte=8
                { 9225, 155 }, // op=0x24 byte=9
                { 9226, 177 }, // op=0x24 byte=10
                { 9227, 363 }, // op=0x24 byte=11
                { 9228, 341 }, // op=0x24 byte=12
                { 9229, 250 }, // op=0x24 byte=13
                { 9230, 285 }, // op=0x24 byte=14
                { 9231, 62 }, // op=0x24 byte=15
                { 9232, 135 }, // op=0x24 byte=16
                { 9233, 119 }, // op=0x24 byte=17
                { 9234, 30 }, // op=0x24 byte=18
                { 9235, 61 }, // op=0x24 byte=19
                { 9236, 38 }, // op=0x24 byte=20
                { 9237, 26 }, // op=0x24 byte=21
                { 9238, 20 }, // op=0x24 byte=22
                { 9239, 48 }, // op=0x24 byte=23
                { 9240, 36 }, // op=0x24 byte=24
                { 9241, 34 }, // op=0x24 byte=25
                { 9242, 65 }, // op=0x24 byte=26
                { 9243, 32 }, // op=0x24 byte=27
                { 9244, 33 }, // op=0x24 byte=28
                { 9245, 40 }, // op=0x24 byte=29
                { 9246, 55 }, // op=0x24 byte=30
                { 9247, 40 }, // op=0x24 byte=31
                { 9248, 44 }, // op=0x24 byte=32
                { 9249, 37 }, // op=0x24 byte=33
                { 9250, 40 }, // op=0x24 byte=34
                { 9251, 38 }, // op=0x24 byte=35
                { 9252, 38 }, // op=0x24 byte=36
                { 9253, 24 }, // op=0x24 byte=37
                { 9254, 16 }, // op=0x24 byte=38
                { 9255, 261 }, // op=0x24 byte=39
                { 9256, 18 }, // op=0x24 byte=40
                { 9257, 14 }, // op=0x24 byte=41
                { 9258, 11 }, // op=0x24 byte=42
                { 9259, 10 }, // op=0x24 byte=43
                { 9260, 221 }, // op=0x24 byte=44
                { 9261, 79 }, // op=0x24 byte=45
                { 9262, 13 }, // op=0x24 byte=46
                { 9263, 13 }, // op=0x24 byte=47
                { 9264, 13 }, // op=0x24 byte=48
                { 9265, 8 }, // op=0x24 byte=49
                { 9266, 11 }, // op=0x24 byte=50
                { 9267, 8 }, // op=0x24 byte=51
                { 9268, 9 }, // op=0x24 byte=52
                { 9269, 11 }, // op=0x24 byte=53
                { 9270, 7 }, // op=0x24 byte=54
                { 9271, 9 }, // op=0x24 byte=55
                { 9272, 23 }, // op=0x24 byte=56
                { 9273, 12 }, // op=0x24 byte=57
                { 9274, 15 }, // op=0x24 byte=58
                { 9275, 11 }, // op=0x24 byte=59
                { 9276, 11 }, // op=0x24 byte=60
                { 9277, 8 }, // op=0x24 byte=61
                { 9278, 12 }, // op=0x24 byte=62
                { 9279, 10 }, // op=0x24 byte=63
                { 9280, 9 }, // op=0x24 byte=64
                { 9281, 6 }, // op=0x24 byte=65
                { 9282, 6 }, // op=0x24 byte=66
                { 9283, 7 }, // op=0x24 byte=67
                { 9284, 9 }, // op=0x24 byte=68
                { 9285, 8 }, // op=0x24 byte=69
                { 9286, 12 }, // op=0x24 byte=70
                { 9287, 9 }, // op=0x24 byte=71
                { 9288, 61 }, // op=0x24 byte=72
                { 9289, 9 }, // op=0x24 byte=73
                { 9290, 6 }, // op=0x24 byte=74
                { 9291, 5 }, // op=0x24 byte=75
                { 9292, 7 }, // op=0x24 byte=76
                { 9293, 6 }, // op=0x24 byte=77
                { 9294, 6 }, // op=0x24 byte=78
                { 9295, 6 }, // op=0x24 byte=79
                { 9296, 6 }, // op=0x24 byte=80
                { 9297, 6 }, // op=0x24 byte=81
                { 9298, 6 }, // op=0x24 byte=82
                { 9299, 6 }, // op=0x24 byte=83
                { 9300, 6 }, // op=0x24 byte=84
                { 9301, 6 }, // op=0x24 byte=85
                { 9302, 4 }, // op=0x24 byte=86
                { 9303, 5 }, // op=0x24 byte=87
                { 9304, 5 }, // op=0x24 byte=88
                { 9305, 5 }, // op=0x24 byte=89
                { 9306, 5 }, // op=0x24 byte=90
                { 9307, 6 }, // op=0x24 byte=91
                { 9308, 5 }, // op=0x24 byte=92
                { 9309, 6 }, // op=0x24 byte=93
                { 9310, 6 }, // op=0x24 byte=94
                { 9311, 5 }, // op=0x24 byte=95
                { 9312, 4 }, // op=0x24 byte=96
                { 9313, 6 }, // op=0x24 byte=97
                { 9314, 3 }, // op=0x24 byte=98
                { 9315, 2 }, // op=0x24 byte=99
                { 9316, 4 }, // op=0x24 byte=100
                { 9317, 2 }, // op=0x24 byte=101
                { 9318, 1 }, // op=0x24 byte=102
                { 9319, 2 }, // op=0x24 byte=103
                { 9320, 1 }, // op=0x24 byte=104
                { 9321, 1 }, // op=0x24 byte=105
                { 9322, 3 }, // op=0x24 byte=106
                { 9323, 1 }, // op=0x24 byte=107
                { 9324, 1 }, // op=0x24 byte=108
                { 9325, 2 }, // op=0x24 byte=109
                { 9326, 2 }, // op=0x24 byte=110
                { 9327, 2 }, // op=0x24 byte=111
                { 9328, 1 }, // op=0x24 byte=112
                { 9329, 2 }, // op=0x24 byte=113
                { 9330, 1 }, // op=0x24 byte=114
                { 9331, 1 }, // op=0x24 byte=115
                { 9332, 1 }, // op=0x24 byte=116
                { 9333, 1 }, // op=0x24 byte=117
                { 9334, 1 }, // op=0x24 byte=118
                { 9335, 5 }, // op=0x24 byte=119
                { 9336, 3 }, // op=0x24 byte=120
                { 9337, 3 }, // op=0x24 byte=121
                { 9338, 3 }, // op=0x24 byte=122
                { 9339, 3 }, // op=0x24 byte=123
                { 9340, 3 }, // op=0x24 byte=124
                { 9341, 3 }, // op=0x24 byte=125
                { 9342, 3 }, // op=0x24 byte=126
                { 9343, 3 }, // op=0x24 byte=127
                { 9344, 6 }, // op=0x24 byte=128
                { 9345, 11 }, // op=0x24 byte=129
                { 9346, 3 }, // op=0x24 byte=130
                { 9347, 6 }, // op=0x24 byte=131
                { 9348, 1 }, // op=0x24 byte=132
                { 9349, 6 }, // op=0x24 byte=133
                { 9350, 1 }, // op=0x24 byte=134
                { 9351, 46 }, // op=0x24 byte=135
                { 9352, 2 }, // op=0x24 byte=136
                { 9353, 6 }, // op=0x24 byte=137
                { 9354, 2 }, // op=0x24 byte=138
                { 9355, 2 }, // op=0x24 byte=139
                { 9356, 1 }, // op=0x24 byte=140
                { 9357, 2 }, // op=0x24 byte=141
                { 9358, 1 }, // op=0x24 byte=142
                { 9359, 1 }, // op=0x24 byte=143
                { 9360, 2 }, // op=0x24 byte=144
                { 9361, 1 }, // op=0x24 byte=145
                { 9362, 1 }, // op=0x24 byte=146
                { 9363, 3 }, // op=0x24 byte=147
                { 9364, 3 }, // op=0x24 byte=148
                { 9365, 1 }, // op=0x24 byte=149
                { 9366, 3 }, // op=0x24 byte=150
                { 9367, 2 }, // op=0x24 byte=151
                { 9368, 3 }, // op=0x24 byte=152
                { 9369, 3 }, // op=0x24 byte=153
                { 9370, 3 }, // op=0x24 byte=154
                { 9371, 5 }, // op=0x24 byte=155
                { 9372, 3 }, // op=0x24 byte=156
                { 9373, 3 }, // op=0x24 byte=157
                { 9374, 3 }, // op=0x24 byte=158
                { 9375, 3 }, // op=0x24 byte=159
                { 9376, 1 }, // op=0x24 byte=160
                { 9377, 1 }, // op=0x24 byte=161
                { 9378, 1 }, // op=0x24 byte=162
                { 9379, 1 }, // op=0x24 byte=163
                { 9380, 1 }, // op=0x24 byte=164
                { 9381, 1 }, // op=0x24 byte=165
                { 9382, 1 }, // op=0x24 byte=166
                { 9383, 1 }, // op=0x24 byte=167
                { 9384, 1 }, // op=0x24 byte=168
                { 9385, 1 }, // op=0x24 byte=169
                { 9386, 1 }, // op=0x24 byte=170
                { 9387, 1 }, // op=0x24 byte=171
                { 9388, 1 }, // op=0x24 byte=172
                { 9389, 1 }, // op=0x24 byte=173
                { 9390, 1 }, // op=0x24 byte=174
                { 9391, 1 }, // op=0x24 byte=175
                { 9392, 1 }, // op=0x24 byte=176
                { 9393, 1 }, // op=0x24 byte=177
                { 9394, 1 }, // op=0x24 byte=178
                { 9395, 1 }, // op=0x24 byte=179
                { 9396, 1 }, // op=0x24 byte=180
                { 9397, 1 }, // op=0x24 byte=181
                { 9398, 1 }, // op=0x24 byte=182
                { 9399, 1 }, // op=0x24 byte=183
                { 9400, 1 }, // op=0x24 byte=184
                { 9401, 1 }, // op=0x24 byte=185
                { 9402, 9 }, // op=0x24 byte=186
                { 9403, 9 }, // op=0x24 byte=187
                { 9404, 9 }, // op=0x24 byte=188
                { 9405, 18 }, // op=0x24 byte=189
                { 9406, 1 }, // op=0x24 byte=190
                { 9407, 1 }, // op=0x24 byte=191
                { 9408, 1 }, // op=0x24 byte=192
                { 9409, 1 }, // op=0x24 byte=193
                { 9410, 1 }, // op=0x24 byte=194
                { 9411, 1 }, // op=0x24 byte=195
                { 9412, 1 }, // op=0x24 byte=196
                { 9413, 7 }, // op=0x24 byte=197
                { 9414, 6 }, // op=0x24 byte=198
                { 9416, 1 }, // op=0x24 byte=200
                { 9417, 1 }, // op=0x24 byte=201
                { 9418, 1 }, // op=0x24 byte=202
                { 9419, 1 }, // op=0x24 byte=203
                { 9420, 1 }, // op=0x24 byte=204
                { 9421, 1 }, // op=0x24 byte=205
                { 9422, 1 }, // op=0x24 byte=206
                { 9423, 1 }, // op=0x24 byte=207
                { 9424, 1 }, // op=0x24 byte=208
                { 9425, 2 }, // op=0x24 byte=209
                { 9426, 1 }, // op=0x24 byte=210
                { 9427, 5 }, // op=0x24 byte=211
                { 9428, 5 }, // op=0x24 byte=212
                { 9429, 4 }, // op=0x24 byte=213
                { 9430, 4 }, // op=0x24 byte=214
                { 9431, 1 }, // op=0x24 byte=215
                { 9432, 5 }, // op=0x24 byte=216
                { 9433, 1 }, // op=0x24 byte=217
                { 9435, 1 }, // op=0x24 byte=219
                { 9472, 260 }, // op=0x25 byte=0
                { 9473, 10 }, // op=0x25 byte=1
                { 9477, 2 }, // op=0x25 byte=5
                { 9479, 14 }, // op=0x25 byte=7
                { 9480, 8 }, // op=0x25 byte=8
                { 9497, 2 }, // op=0x25 byte=25
                { 9727, 32 }, // op=0x25 byte=255
                { 10496, 8332 }, // op=0x29 byte=0
                { 10497, 1694 }, // op=0x29 byte=1
                { 10498, 690 }, // op=0x29 byte=2
                { 10499, 1548 }, // op=0x29 byte=3
                { 10500, 92 }, // op=0x29 byte=4
                { 10501, 194 }, // op=0x29 byte=5
                { 10502, 609 }, // op=0x29 byte=6
                { 10503, 462 }, // op=0x29 byte=7
                { 10504, 115 }, // op=0x29 byte=8
                { 10505, 10 }, // op=0x29 byte=9
                { 10506, 35 }, // op=0x29 byte=10
                { 10507, 4 }, // op=0x29 byte=11
                { 10509, 17 }, // op=0x29 byte=13
                { 10510, 31 }, // op=0x29 byte=14
                { 10511, 55 }, // op=0x29 byte=15
                { 10512, 58 }, // op=0x29 byte=16
                { 10513, 160 }, // op=0x29 byte=17
                { 10514, 65 }, // op=0x29 byte=18
                { 10515, 45 }, // op=0x29 byte=19
                { 10516, 3 }, // op=0x29 byte=20
                { 10517, 14 }, // op=0x29 byte=21
                { 10518, 14 }, // op=0x29 byte=22
                { 10519, 14 }, // op=0x29 byte=23
                { 10523, 1 }, // op=0x29 byte=27
                { 10524, 1 }, // op=0x29 byte=28
                { 10526, 86 }, // op=0x29 byte=30
                { 10527, 136 }, // op=0x29 byte=31
                { 10529, 30 }, // op=0x29 byte=33
                { 10530, 6 }, // op=0x29 byte=34
                { 10531, 8 }, // op=0x29 byte=35
                { 10532, 9 }, // op=0x29 byte=36
                { 10533, 6 }, // op=0x29 byte=37
                { 10535, 2 }, // op=0x29 byte=39
                { 10536, 6 }, // op=0x29 byte=40
                { 10540, 3 }, // op=0x29 byte=44
                { 10546, 134 }, // op=0x29 byte=50
                { 10553, 1 }, // op=0x29 byte=57
                { 10554, 2 }, // op=0x29 byte=58
                { 10555, 1 }, // op=0x29 byte=59
                { 10556, 102 }, // op=0x29 byte=60
                { 10557, 1 }, // op=0x29 byte=61
                { 10558, 1 }, // op=0x29 byte=62
                { 10559, 1 }, // op=0x29 byte=63
                { 10560, 1 }, // op=0x29 byte=64
                { 10561, 1 }, // op=0x29 byte=65
                { 10562, 1 }, // op=0x29 byte=66
                { 10563, 1 }, // op=0x29 byte=67
                { 10564, 1 }, // op=0x29 byte=68
                { 10565, 1 }, // op=0x29 byte=69
                { 10566, 1 }, // op=0x29 byte=70
                { 10567, 1 }, // op=0x29 byte=71
                { 10568, 2 }, // op=0x29 byte=72
                { 10569, 5 }, // op=0x29 byte=73
                { 10571, 13 }, // op=0x29 byte=75
                { 10576, 1 }, // op=0x29 byte=80
                { 10596, 780 }, // op=0x29 byte=100
                { 10616, 18 }, // op=0x29 byte=120
                { 10646, 72 }, // op=0x29 byte=150
                { 10696, 3 }, // op=0x29 byte=200
                { 10740, 74 }, // op=0x29 byte=244
                { 10750, 4 }, // op=0x29 byte=254
                { 10751, 806 }, // op=0x29 byte=255
                { 10752, 7599 }, // op=0x2a byte=0
                { 10753, 319 }, // op=0x2a byte=1
                { 10754, 620 }, // op=0x2a byte=2
                { 10755, 35 }, // op=0x2a byte=3
                { 10756, 198 }, // op=0x2a byte=4
                { 10757, 300 }, // op=0x2a byte=5
                { 10758, 1454 }, // op=0x2a byte=6
                { 10759, 95 }, // op=0x2a byte=7
                { 10760, 155 }, // op=0x2a byte=8
                { 10761, 62 }, // op=0x2a byte=9
                { 10762, 44 }, // op=0x2a byte=10
                { 10763, 33 }, // op=0x2a byte=11
                { 10764, 41 }, // op=0x2a byte=12
                { 10765, 32 }, // op=0x2a byte=13
                { 10766, 31 }, // op=0x2a byte=14
                { 10767, 22 }, // op=0x2a byte=15
                { 10768, 107 }, // op=0x2a byte=16
                { 10769, 24 }, // op=0x2a byte=17
                { 10770, 58 }, // op=0x2a byte=18
                { 10771, 33 }, // op=0x2a byte=19
                { 10772, 66 }, // op=0x2a byte=20
                { 10773, 55 }, // op=0x2a byte=21
                { 10774, 86 }, // op=0x2a byte=22
                { 10775, 115 }, // op=0x2a byte=23
                { 10776, 24 }, // op=0x2a byte=24
                { 10777, 25 }, // op=0x2a byte=25
                { 10778, 25 }, // op=0x2a byte=26
                { 10779, 48 }, // op=0x2a byte=27
                { 10780, 38 }, // op=0x2a byte=28
                { 10781, 35 }, // op=0x2a byte=29
                { 10782, 44 }, // op=0x2a byte=30
                { 10783, 62 }, // op=0x2a byte=31
                { 10784, 153 }, // op=0x2a byte=32
                { 10785, 158 }, // op=0x2a byte=33
                { 10786, 25 }, // op=0x2a byte=34
                { 10787, 84 }, // op=0x2a byte=35
                { 10788, 46 }, // op=0x2a byte=36
                { 10789, 45 }, // op=0x2a byte=37
                { 10790, 26 }, // op=0x2a byte=38
                { 10791, 27 }, // op=0x2a byte=39
                { 10792, 24 }, // op=0x2a byte=40
                { 10793, 73 }, // op=0x2a byte=41
                { 10794, 92 }, // op=0x2a byte=42
                { 10795, 15 }, // op=0x2a byte=43
                { 10796, 30 }, // op=0x2a byte=44
                { 10797, 15 }, // op=0x2a byte=45
                { 10798, 34 }, // op=0x2a byte=46
                { 10799, 16 }, // op=0x2a byte=47
                { 10800, 51 }, // op=0x2a byte=48
                { 10801, 21 }, // op=0x2a byte=49
                { 10802, 34 }, // op=0x2a byte=50
                { 10803, 150 }, // op=0x2a byte=51
                { 10804, 28 }, // op=0x2a byte=52
                { 10805, 14 }, // op=0x2a byte=53
                { 10806, 52 }, // op=0x2a byte=54
                { 10807, 12 }, // op=0x2a byte=55
                { 10808, 14 }, // op=0x2a byte=56
                { 10809, 30 }, // op=0x2a byte=57
                { 10810, 18 }, // op=0x2a byte=58
                { 10811, 29 }, // op=0x2a byte=59
                { 10812, 28 }, // op=0x2a byte=60
                { 10813, 12 }, // op=0x2a byte=61
                { 10814, 38 }, // op=0x2a byte=62
                { 10815, 18 }, // op=0x2a byte=63
                { 10816, 25 }, // op=0x2a byte=64
                { 10817, 18 }, // op=0x2a byte=65
                { 10818, 32 }, // op=0x2a byte=66
                { 10819, 45 }, // op=0x2a byte=67
                { 10820, 34 }, // op=0x2a byte=68
                { 10821, 29 }, // op=0x2a byte=69
                { 10822, 23 }, // op=0x2a byte=70
                { 10823, 34 }, // op=0x2a byte=71
                { 10824, 39 }, // op=0x2a byte=72
                { 10825, 9 }, // op=0x2a byte=73
                { 10826, 27 }, // op=0x2a byte=74
                { 10827, 24 }, // op=0x2a byte=75
                { 10828, 25 }, // op=0x2a byte=76
                { 10829, 43 }, // op=0x2a byte=77
                { 10830, 25 }, // op=0x2a byte=78
                { 10831, 48 }, // op=0x2a byte=79
                { 10832, 76 }, // op=0x2a byte=80
                { 10833, 78 }, // op=0x2a byte=81
                { 10834, 88 }, // op=0x2a byte=82
                { 10835, 51 }, // op=0x2a byte=83
                { 10836, 48 }, // op=0x2a byte=84
                { 10837, 28 }, // op=0x2a byte=85
                { 10838, 46 }, // op=0x2a byte=86
                { 10839, 15 }, // op=0x2a byte=87
                { 10840, 47 }, // op=0x2a byte=88
                { 10841, 28 }, // op=0x2a byte=89
                { 10842, 80 }, // op=0x2a byte=90
                { 10843, 22 }, // op=0x2a byte=91
                { 10844, 96 }, // op=0x2a byte=92
                { 10845, 15 }, // op=0x2a byte=93
                { 10846, 22 }, // op=0x2a byte=94
                { 10847, 32 }, // op=0x2a byte=95
                { 10848, 18 }, // op=0x2a byte=96
                { 10849, 62 }, // op=0x2a byte=97
                { 10850, 31 }, // op=0x2a byte=98
                { 10851, 19 }, // op=0x2a byte=99
                { 10852, 38 }, // op=0x2a byte=100
                { 10853, 18 }, // op=0x2a byte=101
                { 10854, 49 }, // op=0x2a byte=102
                { 10855, 41 }, // op=0x2a byte=103
                { 10856, 16 }, // op=0x2a byte=104
                { 10857, 65 }, // op=0x2a byte=105
                { 10858, 20 }, // op=0x2a byte=106
                { 10859, 23 }, // op=0x2a byte=107
                { 10860, 47 }, // op=0x2a byte=108
                { 10861, 34 }, // op=0x2a byte=109
                { 10862, 18 }, // op=0x2a byte=110
                { 10863, 40 }, // op=0x2a byte=111
                { 10864, 52 }, // op=0x2a byte=112
                { 10865, 45 }, // op=0x2a byte=113
                { 10866, 9 }, // op=0x2a byte=114
                { 10867, 28 }, // op=0x2a byte=115
                { 10868, 34 }, // op=0x2a byte=116
                { 10869, 65 }, // op=0x2a byte=117
                { 10870, 74 }, // op=0x2a byte=118
                { 10871, 13 }, // op=0x2a byte=119
                { 10872, 11 }, // op=0x2a byte=120
                { 10873, 8 }, // op=0x2a byte=121
                { 10874, 24 }, // op=0x2a byte=122
                { 10875, 83 }, // op=0x2a byte=123
                { 10876, 104 }, // op=0x2a byte=124
                { 10877, 12 }, // op=0x2a byte=125
                { 10878, 11 }, // op=0x2a byte=126
                { 10879, 39 }, // op=0x2a byte=127
                { 10880, 72 }, // op=0x2a byte=128
                { 10881, 22 }, // op=0x2a byte=129
                { 10882, 99 }, // op=0x2a byte=130
                { 10883, 12 }, // op=0x2a byte=131
                { 10884, 73 }, // op=0x2a byte=132
                { 10885, 14 }, // op=0x2a byte=133
                { 10886, 26 }, // op=0x2a byte=134
                { 10887, 41 }, // op=0x2a byte=135
                { 10888, 181 }, // op=0x2a byte=136
                { 10889, 62 }, // op=0x2a byte=137
                { 10890, 48 }, // op=0x2a byte=138
                { 10891, 23 }, // op=0x2a byte=139
                { 10892, 36 }, // op=0x2a byte=140
                { 10893, 6 }, // op=0x2a byte=141
                { 10894, 42 }, // op=0x2a byte=142
                { 10895, 36 }, // op=0x2a byte=143
                { 10896, 86 }, // op=0x2a byte=144
                { 10897, 21 }, // op=0x2a byte=145
                { 10898, 22 }, // op=0x2a byte=146
                { 10899, 31 }, // op=0x2a byte=147
                { 10900, 43 }, // op=0x2a byte=148
                { 10901, 39 }, // op=0x2a byte=149
                { 10902, 31 }, // op=0x2a byte=150
                { 10903, 10 }, // op=0x2a byte=151
                { 10904, 18 }, // op=0x2a byte=152
                { 10905, 74 }, // op=0x2a byte=153
                { 10906, 66 }, // op=0x2a byte=154
                { 10907, 10 }, // op=0x2a byte=155
                { 10908, 49 }, // op=0x2a byte=156
                { 10909, 31 }, // op=0x2a byte=157
                { 10910, 18 }, // op=0x2a byte=158
                { 10911, 93 }, // op=0x2a byte=159
                { 10912, 13 }, // op=0x2a byte=160
                { 10913, 18 }, // op=0x2a byte=161
                { 10914, 21 }, // op=0x2a byte=162
                { 10915, 54 }, // op=0x2a byte=163
                { 10916, 43 }, // op=0x2a byte=164
                { 10917, 21 }, // op=0x2a byte=165
                { 10918, 30 }, // op=0x2a byte=166
                { 10919, 37 }, // op=0x2a byte=167
                { 10920, 31 }, // op=0x2a byte=168
                { 10921, 121 }, // op=0x2a byte=169
                { 10922, 47 }, // op=0x2a byte=170
                { 10923, 47 }, // op=0x2a byte=171
                { 10924, 20 }, // op=0x2a byte=172
                { 10925, 21 }, // op=0x2a byte=173
                { 10926, 22 }, // op=0x2a byte=174
                { 10927, 51 }, // op=0x2a byte=175
                { 10928, 51 }, // op=0x2a byte=176
                { 10929, 23 }, // op=0x2a byte=177
                { 10930, 15 }, // op=0x2a byte=178
                { 10931, 50 }, // op=0x2a byte=179
                { 10932, 67 }, // op=0x2a byte=180
                { 10933, 13 }, // op=0x2a byte=181
                { 10934, 86 }, // op=0x2a byte=182
                { 10935, 9 }, // op=0x2a byte=183
                { 10936, 34 }, // op=0x2a byte=184
                { 10937, 46 }, // op=0x2a byte=185
                { 10938, 38 }, // op=0x2a byte=186
                { 10939, 26 }, // op=0x2a byte=187
                { 10940, 66 }, // op=0x2a byte=188
                { 10941, 14 }, // op=0x2a byte=189
                { 10942, 21 }, // op=0x2a byte=190
                { 10943, 29 }, // op=0x2a byte=191
                { 10944, 42 }, // op=0x2a byte=192
                { 10945, 29 }, // op=0x2a byte=193
                { 10946, 97 }, // op=0x2a byte=194
                { 10947, 17 }, // op=0x2a byte=195
                { 10948, 60 }, // op=0x2a byte=196
                { 10949, 58 }, // op=0x2a byte=197
                { 10950, 27 }, // op=0x2a byte=198
                { 10951, 49 }, // op=0x2a byte=199
                { 10952, 32 }, // op=0x2a byte=200
                { 10953, 45 }, // op=0x2a byte=201
                { 10954, 22 }, // op=0x2a byte=202
                { 10955, 12 }, // op=0x2a byte=203
                { 10956, 21 }, // op=0x2a byte=204
                { 10957, 18 }, // op=0x2a byte=205
                { 10958, 91 }, // op=0x2a byte=206
                { 10959, 177 }, // op=0x2a byte=207
                { 10960, 21 }, // op=0x2a byte=208
                { 10961, 22 }, // op=0x2a byte=209
                { 10962, 10 }, // op=0x2a byte=210
                { 10963, 8 }, // op=0x2a byte=211
                { 10964, 23 }, // op=0x2a byte=212
                { 10965, 15 }, // op=0x2a byte=213
                { 10966, 19 }, // op=0x2a byte=214
                { 10967, 18 }, // op=0x2a byte=215
                { 10968, 51 }, // op=0x2a byte=216
                { 10969, 48 }, // op=0x2a byte=217
                { 10970, 18 }, // op=0x2a byte=218
                { 10971, 40 }, // op=0x2a byte=219
                { 10972, 25 }, // op=0x2a byte=220
                { 10973, 32 }, // op=0x2a byte=221
                { 10974, 42 }, // op=0x2a byte=222
                { 10975, 54 }, // op=0x2a byte=223
                { 10976, 18 }, // op=0x2a byte=224
                { 10977, 20 }, // op=0x2a byte=225
                { 10978, 63 }, // op=0x2a byte=226
                { 10979, 44 }, // op=0x2a byte=227
                { 10980, 34 }, // op=0x2a byte=228
                { 10981, 29 }, // op=0x2a byte=229
                { 10982, 24 }, // op=0x2a byte=230
                { 10983, 41 }, // op=0x2a byte=231
                { 10984, 44 }, // op=0x2a byte=232
                { 10985, 17 }, // op=0x2a byte=233
                { 10986, 67 }, // op=0x2a byte=234
                { 10987, 41 }, // op=0x2a byte=235
                { 10988, 154 }, // op=0x2a byte=236
                { 10989, 30 }, // op=0x2a byte=237
                { 10990, 18 }, // op=0x2a byte=238
                { 10991, 42 }, // op=0x2a byte=239
                { 10992, 19 }, // op=0x2a byte=240
                { 10993, 24 }, // op=0x2a byte=241
                { 10994, 33 }, // op=0x2a byte=242
                { 10995, 67 }, // op=0x2a byte=243
                { 10996, 127 }, // op=0x2a byte=244
                { 10997, 21 }, // op=0x2a byte=245
                { 10998, 54 }, // op=0x2a byte=246
                { 10999, 47 }, // op=0x2a byte=247
                { 11000, 17 }, // op=0x2a byte=248
                { 11001, 41 }, // op=0x2a byte=249
                { 11002, 9 }, // op=0x2a byte=250
                { 11003, 56 }, // op=0x2a byte=251
                { 11004, 149 }, // op=0x2a byte=252
                { 11005, 53 }, // op=0x2a byte=253
                { 11006, 45 }, // op=0x2a byte=254
                { 11007, 133 }, // op=0x2a byte=255
                { 11008, 2151 }, // op=0x2b byte=0
                { 11009, 74 }, // op=0x2b byte=1
                { 11010, 7 }, // op=0x2b byte=2
                { 11264, 131718 }, // op=0x2c byte=0
                { 11265, 7731 }, // op=0x2c byte=1
                { 11266, 6580 }, // op=0x2c byte=2
                { 11267, 4483 }, // op=0x2c byte=3
                { 11268, 1075 }, // op=0x2c byte=4
                { 11269, 852 }, // op=0x2c byte=5
                { 11270, 774 }, // op=0x2c byte=6
                { 11271, 591 }, // op=0x2c byte=7
                { 11272, 746 }, // op=0x2c byte=8
                { 11273, 561 }, // op=0x2c byte=9
                { 11274, 543 }, // op=0x2c byte=10
                { 11275, 652 }, // op=0x2c byte=11
                { 11276, 5722 }, // op=0x2c byte=12
                { 11277, 319 }, // op=0x2c byte=13
                { 11278, 302 }, // op=0x2c byte=14
                { 11279, 293 }, // op=0x2c byte=15
                { 11280, 861 }, // op=0x2c byte=16
                { 11281, 361 }, // op=0x2c byte=17
                { 11282, 218 }, // op=0x2c byte=18
                { 11283, 294 }, // op=0x2c byte=19
                { 11284, 235 }, // op=0x2c byte=20
                { 11285, 371 }, // op=0x2c byte=21
                { 11286, 224 }, // op=0x2c byte=22
                { 11287, 215 }, // op=0x2c byte=23
                { 11288, 201 }, // op=0x2c byte=24
                { 11289, 219 }, // op=0x2c byte=25
                { 11290, 186 }, // op=0x2c byte=26
                { 11291, 225 }, // op=0x2c byte=27
                { 11292, 188 }, // op=0x2c byte=28
                { 11293, 131 }, // op=0x2c byte=29
                { 11294, 132 }, // op=0x2c byte=30
                { 11295, 129 }, // op=0x2c byte=31
                { 11296, 10459 }, // op=0x2c byte=32
                { 11297, 113 }, // op=0x2c byte=33
                { 11298, 319 }, // op=0x2c byte=34
                { 11299, 99 }, // op=0x2c byte=35
                { 11300, 179 }, // op=0x2c byte=36
                { 11301, 97 }, // op=0x2c byte=37
                { 11302, 67 }, // op=0x2c byte=38
                { 11303, 75 }, // op=0x2c byte=39
                { 11304, 219 }, // op=0x2c byte=40
                { 11305, 154 }, // op=0x2c byte=41
                { 11306, 132 }, // op=0x2c byte=42
                { 11307, 97 }, // op=0x2c byte=43
                { 11308, 70 }, // op=0x2c byte=44
                { 11309, 72 }, // op=0x2c byte=45
                { 11310, 50 }, // op=0x2c byte=46
                { 11311, 144 }, // op=0x2c byte=47
                { 11312, 258 }, // op=0x2c byte=48
                { 11313, 362 }, // op=0x2c byte=49
                { 11314, 108 }, // op=0x2c byte=50
                { 11315, 93 }, // op=0x2c byte=51
                { 11316, 106 }, // op=0x2c byte=52
                { 11317, 66 }, // op=0x2c byte=53
                { 11318, 85 }, // op=0x2c byte=54
                { 11319, 119 }, // op=0x2c byte=55
                { 11320, 177 }, // op=0x2c byte=56
                { 11321, 103 }, // op=0x2c byte=57
                { 11322, 131 }, // op=0x2c byte=58
                { 11323, 145 }, // op=0x2c byte=59
                { 11324, 128 }, // op=0x2c byte=60
                { 11325, 141 }, // op=0x2c byte=61
                { 11326, 172 }, // op=0x2c byte=62
                { 11327, 152 }, // op=0x2c byte=63
                { 11328, 462 }, // op=0x2c byte=64
                { 11329, 158 }, // op=0x2c byte=65
                { 11330, 102 }, // op=0x2c byte=66
                { 11331, 63 }, // op=0x2c byte=67
                { 11332, 44 }, // op=0x2c byte=68
                { 11333, 38 }, // op=0x2c byte=69
                { 11334, 63 }, // op=0x2c byte=70
                { 11335, 71 }, // op=0x2c byte=71
                { 11336, 87 }, // op=0x2c byte=72
                { 11337, 89 }, // op=0x2c byte=73
                { 11338, 71 }, // op=0x2c byte=74
                { 11339, 90 }, // op=0x2c byte=75
                { 11340, 61 }, // op=0x2c byte=76
                { 11341, 30 }, // op=0x2c byte=77
                { 11342, 113 }, // op=0x2c byte=78
                { 11343, 48 }, // op=0x2c byte=79
                { 11344, 34 }, // op=0x2c byte=80
                { 11345, 35 }, // op=0x2c byte=81
                { 11346, 42 }, // op=0x2c byte=82
                { 11347, 42 }, // op=0x2c byte=83
                { 11348, 48 }, // op=0x2c byte=84
                { 11349, 38 }, // op=0x2c byte=85
                { 11350, 45 }, // op=0x2c byte=86
                { 11351, 32 }, // op=0x2c byte=87
                { 11352, 31 }, // op=0x2c byte=88
                { 11353, 26 }, // op=0x2c byte=89
                { 11354, 39 }, // op=0x2c byte=90
                { 11355, 56 }, // op=0x2c byte=91
                { 11356, 46 }, // op=0x2c byte=92
                { 11357, 40 }, // op=0x2c byte=93
                { 11358, 83 }, // op=0x2c byte=94
                { 11359, 33 }, // op=0x2c byte=95
                { 11360, 184 }, // op=0x2c byte=96
                { 11361, 28 }, // op=0x2c byte=97
                { 11362, 38 }, // op=0x2c byte=98
                { 11363, 19 }, // op=0x2c byte=99
                { 11364, 39 }, // op=0x2c byte=100
                { 11365, 55 }, // op=0x2c byte=101
                { 11366, 62 }, // op=0x2c byte=102
                { 11367, 49 }, // op=0x2c byte=103
                { 11368, 47 }, // op=0x2c byte=104
                { 11369, 35 }, // op=0x2c byte=105
                { 11370, 37 }, // op=0x2c byte=106
                { 11371, 32 }, // op=0x2c byte=107
                { 11372, 35 }, // op=0x2c byte=108
                { 11373, 34 }, // op=0x2c byte=109
                { 11374, 60 }, // op=0x2c byte=110
                { 11375, 115 }, // op=0x2c byte=111
                { 11376, 52 }, // op=0x2c byte=112
                { 11377, 37 }, // op=0x2c byte=113
                { 11378, 44 }, // op=0x2c byte=114
                { 11379, 42 }, // op=0x2c byte=115
                { 11380, 20 }, // op=0x2c byte=116
                { 11381, 20 }, // op=0x2c byte=117
                { 11382, 19 }, // op=0x2c byte=118
                { 11383, 31 }, // op=0x2c byte=119
                { 11384, 55 }, // op=0x2c byte=120
                { 11385, 37 }, // op=0x2c byte=121
                { 11386, 52 }, // op=0x2c byte=122
                { 11387, 35 }, // op=0x2c byte=123
                { 11388, 59 }, // op=0x2c byte=124
                { 11389, 36 }, // op=0x2c byte=125
                { 11390, 20 }, // op=0x2c byte=126
                { 11391, 27 }, // op=0x2c byte=127
                { 11392, 113 }, // op=0x2c byte=128
                { 11393, 20 }, // op=0x2c byte=129
                { 11394, 33 }, // op=0x2c byte=130
                { 11395, 33 }, // op=0x2c byte=131
                { 11396, 13 }, // op=0x2c byte=132
                { 11397, 59 }, // op=0x2c byte=133
                { 11398, 29 }, // op=0x2c byte=134
                { 11399, 49 }, // op=0x2c byte=135
                { 11400, 32 }, // op=0x2c byte=136
                { 11401, 33 }, // op=0x2c byte=137
                { 11402, 33 }, // op=0x2c byte=138
                { 11403, 21 }, // op=0x2c byte=139
                { 11404, 50 }, // op=0x2c byte=140
                { 11405, 62 }, // op=0x2c byte=141
                { 11406, 40 }, // op=0x2c byte=142
                { 11407, 67 }, // op=0x2c byte=143
                { 11408, 100 }, // op=0x2c byte=144
                { 11409, 37 }, // op=0x2c byte=145
                { 11410, 17 }, // op=0x2c byte=146
                { 11411, 19 }, // op=0x2c byte=147
                { 11412, 17 }, // op=0x2c byte=148
                { 11413, 37 }, // op=0x2c byte=149
                { 11414, 26 }, // op=0x2c byte=150
                { 11415, 26 }, // op=0x2c byte=151
                { 11416, 27 }, // op=0x2c byte=152
                { 11417, 13 }, // op=0x2c byte=153
                { 11418, 14 }, // op=0x2c byte=154
                { 11419, 34 }, // op=0x2c byte=155
                { 11420, 35 }, // op=0x2c byte=156
                { 11421, 33 }, // op=0x2c byte=157
                { 11422, 122 }, // op=0x2c byte=158
                { 11423, 15 }, // op=0x2c byte=159
                { 11424, 205 }, // op=0x2c byte=160
                { 11425, 48 }, // op=0x2c byte=161
                { 11426, 47 }, // op=0x2c byte=162
                { 11427, 30 }, // op=0x2c byte=163
                { 11428, 41 }, // op=0x2c byte=164
                { 11429, 40 }, // op=0x2c byte=165
                { 11430, 26 }, // op=0x2c byte=166
                { 11431, 23 }, // op=0x2c byte=167
                { 11432, 25 }, // op=0x2c byte=168
                { 11433, 25 }, // op=0x2c byte=169
                { 11434, 24 }, // op=0x2c byte=170
                { 11435, 22 }, // op=0x2c byte=171
                { 11436, 27 }, // op=0x2c byte=172
                { 11437, 26 }, // op=0x2c byte=173
                { 11438, 63 }, // op=0x2c byte=174
                { 11439, 63 }, // op=0x2c byte=175
                { 11440, 14 }, // op=0x2c byte=176
                { 11441, 16 }, // op=0x2c byte=177
                { 11442, 16 }, // op=0x2c byte=178
                { 11443, 16 }, // op=0x2c byte=179
                { 11444, 38 }, // op=0x2c byte=180
                { 11445, 37 }, // op=0x2c byte=181
                { 11446, 13 }, // op=0x2c byte=182
                { 11447, 15 }, // op=0x2c byte=183
                { 11448, 11 }, // op=0x2c byte=184
                { 11449, 37 }, // op=0x2c byte=185
                { 11450, 58 }, // op=0x2c byte=186
                { 11451, 33 }, // op=0x2c byte=187
                { 11452, 31 }, // op=0x2c byte=188
                { 11453, 47 }, // op=0x2c byte=189
                { 11454, 122 }, // op=0x2c byte=190
                { 11455, 59 }, // op=0x2c byte=191
                { 11456, 9 }, // op=0x2c byte=192
                { 11457, 37 }, // op=0x2c byte=193
                { 11458, 36 }, // op=0x2c byte=194
                { 11459, 19 }, // op=0x2c byte=195
                { 11460, 14 }, // op=0x2c byte=196
                { 11461, 19 }, // op=0x2c byte=197
                { 11462, 9 }, // op=0x2c byte=198
                { 11463, 15 }, // op=0x2c byte=199
                { 11464, 58 }, // op=0x2c byte=200
                { 11465, 63 }, // op=0x2c byte=201
                { 11466, 23 }, // op=0x2c byte=202
                { 11467, 23 }, // op=0x2c byte=203
                { 11468, 21 }, // op=0x2c byte=204
                { 11469, 19 }, // op=0x2c byte=205
                { 11470, 22 }, // op=0x2c byte=206
                { 11471, 22 }, // op=0x2c byte=207
                { 11472, 21 }, // op=0x2c byte=208
                { 11473, 18 }, // op=0x2c byte=209
                { 11474, 73 }, // op=0x2c byte=210
                { 11475, 222 }, // op=0x2c byte=211
                { 11476, 75 }, // op=0x2c byte=212
                { 11477, 18 }, // op=0x2c byte=213
                { 11478, 17 }, // op=0x2c byte=214
                { 11479, 21 }, // op=0x2c byte=215
                { 11480, 20 }, // op=0x2c byte=216
                { 11481, 20 }, // op=0x2c byte=217
                { 11482, 19 }, // op=0x2c byte=218
                { 11483, 23 }, // op=0x2c byte=219
                { 11484, 17 }, // op=0x2c byte=220
                { 11485, 23 }, // op=0x2c byte=221
                { 11486, 13 }, // op=0x2c byte=222
                { 11487, 13 }, // op=0x2c byte=223
                { 11488, 12 }, // op=0x2c byte=224
                { 11489, 17 }, // op=0x2c byte=225
                { 11490, 12 }, // op=0x2c byte=226
                { 11491, 15 }, // op=0x2c byte=227
                { 11492, 11 }, // op=0x2c byte=228
                { 11493, 11 }, // op=0x2c byte=229
                { 11494, 37 }, // op=0x2c byte=230
                { 11495, 35 }, // op=0x2c byte=231
                { 11496, 32 }, // op=0x2c byte=232
                { 11497, 41 }, // op=0x2c byte=233
                { 11498, 30 }, // op=0x2c byte=234
                { 11499, 35 }, // op=0x2c byte=235
                { 11500, 34 }, // op=0x2c byte=236
                { 11501, 35 }, // op=0x2c byte=237
                { 11502, 35 }, // op=0x2c byte=238
                { 11503, 24 }, // op=0x2c byte=239
                { 11504, 24 }, // op=0x2c byte=240
                { 11505, 31 }, // op=0x2c byte=241
                { 11506, 24 }, // op=0x2c byte=242
                { 11507, 39 }, // op=0x2c byte=243
                { 11508, 30 }, // op=0x2c byte=244
                { 11509, 18 }, // op=0x2c byte=245
                { 11510, 14 }, // op=0x2c byte=246
                { 11511, 12 }, // op=0x2c byte=247
                { 11512, 10 }, // op=0x2c byte=248
                { 11513, 12 }, // op=0x2c byte=249
                { 11514, 43 }, // op=0x2c byte=250
                { 11515, 20 }, // op=0x2c byte=251
                { 11516, 17 }, // op=0x2c byte=252
                { 11517, 13 }, // op=0x2c byte=253
                { 11518, 12 }, // op=0x2c byte=254
                { 11519, 11 }, // op=0x2c byte=255
                { 11520, 14621 }, // op=0x2d byte=0
                { 11521, 1835 }, // op=0x2d byte=1
                { 11522, 1709 }, // op=0x2d byte=2
                { 11523, 192 }, // op=0x2d byte=3
                { 11524, 510 }, // op=0x2d byte=4
                { 11525, 27 }, // op=0x2d byte=5
                { 11526, 90 }, // op=0x2d byte=6
                { 11527, 63 }, // op=0x2d byte=7
                { 11528, 347 }, // op=0x2d byte=8
                { 11529, 171 }, // op=0x2d byte=9
                { 11530, 22 }, // op=0x2d byte=10
                { 11531, 73 }, // op=0x2d byte=11
                { 11532, 1 }, // op=0x2d byte=12
                { 11533, 2 }, // op=0x2d byte=13
                { 11534, 13 }, // op=0x2d byte=14
                { 11535, 13 }, // op=0x2d byte=15
                { 11545, 12 }, // op=0x2d byte=25
                { 11546, 2 }, // op=0x2d byte=26
                { 11547, 1 }, // op=0x2d byte=27
                { 11776, 5716 }, // op=0x2e byte=0
                { 11777, 156 }, // op=0x2e byte=1
                { 11778, 442 }, // op=0x2e byte=2
                { 11779, 816 }, // op=0x2e byte=3
                { 11780, 209 }, // op=0x2e byte=4
                { 11781, 69 }, // op=0x2e byte=5
                { 11782, 71 }, // op=0x2e byte=6
                { 11783, 2 }, // op=0x2e byte=7
                { 11784, 3 }, // op=0x2e byte=8
                { 11786, 2 }, // op=0x2e byte=10
                { 11788, 2 }, // op=0x2e byte=12
                { 11789, 1 }, // op=0x2e byte=13
                { 11790, 1 }, // op=0x2e byte=14
                { 11794, 12 }, // op=0x2e byte=18
                { 11795, 12 }, // op=0x2e byte=19
                { 11797, 6 }, // op=0x2e byte=21
                { 11799, 12 }, // op=0x2e byte=23
                { 11800, 12 }, // op=0x2e byte=24
                { 12031, 8 }, // op=0x2e byte=255
                { 12032, 885 }, // op=0x2f byte=0
                { 12033, 205 }, // op=0x2f byte=1
                { 12034, 46 }, // op=0x2f byte=2
                { 12288, 7736 }, // op=0x30 byte=0
                { 12289, 432 }, // op=0x30 byte=1
                { 12543, 1312 }, // op=0x30 byte=255
                { 12544, 2208 }, // op=0x31 byte=0
                { 12799, 4416 }, // op=0x31 byte=255
                { 12800, 6465 }, // op=0x32 byte=0
                { 12801, 1629 }, // op=0x32 byte=1
                { 12802, 155 }, // op=0x32 byte=2
                { 12803, 112 }, // op=0x32 byte=3
                { 12804, 97 }, // op=0x32 byte=4
                { 12805, 123 }, // op=0x32 byte=5
                { 12806, 66 }, // op=0x32 byte=6
                { 12807, 42 }, // op=0x32 byte=7
                { 12808, 75 }, // op=0x32 byte=8
                { 12809, 66 }, // op=0x32 byte=9
                { 12810, 36 }, // op=0x32 byte=10
                { 12811, 27 }, // op=0x32 byte=11
                { 12812, 20 }, // op=0x32 byte=12
                { 12813, 33 }, // op=0x32 byte=13
                { 12814, 11 }, // op=0x32 byte=14
                { 12815, 17 }, // op=0x32 byte=15
                { 12816, 11 }, // op=0x32 byte=16
                { 12817, 7 }, // op=0x32 byte=17
                { 12818, 6 }, // op=0x32 byte=18
                { 12819, 6 }, // op=0x32 byte=19
                { 12820, 5 }, // op=0x32 byte=20
                { 12821, 5 }, // op=0x32 byte=21
                { 12822, 5 }, // op=0x32 byte=22
                { 12823, 5 }, // op=0x32 byte=23
                { 12824, 5 }, // op=0x32 byte=24
                { 12825, 3 }, // op=0x32 byte=25
                { 12826, 6 }, // op=0x32 byte=26
                { 12827, 4 }, // op=0x32 byte=27
                { 12828, 3 }, // op=0x32 byte=28
                { 12829, 3 }, // op=0x32 byte=29
                { 12830, 3 }, // op=0x32 byte=30
                { 12846, 1293 }, // op=0x32 byte=46
                { 13056, 2357 }, // op=0x33 byte=0
                { 13057, 2516 }, // op=0x33 byte=1
                { 13058, 233 }, // op=0x33 byte=2
                { 13059, 2141 }, // op=0x33 byte=3
                { 13060, 579 }, // op=0x33 byte=4
                { 13061, 514 }, // op=0x33 byte=5
                { 13062, 1218 }, // op=0x33 byte=6
                { 13063, 578 }, // op=0x33 byte=7
                { 13064, 755 }, // op=0x33 byte=8
                { 13065, 2227 }, // op=0x33 byte=9
                { 13066, 28 }, // op=0x33 byte=10
                { 13067, 118 }, // op=0x33 byte=11
                { 13068, 87 }, // op=0x33 byte=12
                { 13069, 1189 }, // op=0x33 byte=13
                { 13070, 17 }, // op=0x33 byte=14
                { 13071, 117 }, // op=0x33 byte=15
                { 13072, 68 }, // op=0x33 byte=16
                { 13073, 1350 }, // op=0x33 byte=17
                { 13074, 121 }, // op=0x33 byte=18
                { 13075, 37 }, // op=0x33 byte=19
                { 13076, 115 }, // op=0x33 byte=20
                { 13077, 56 }, // op=0x33 byte=21
                { 13078, 41 }, // op=0x33 byte=22
                { 13079, 75 }, // op=0x33 byte=23
                { 13080, 136 }, // op=0x33 byte=24
                { 13081, 95 }, // op=0x33 byte=25
                { 13082, 69 }, // op=0x33 byte=26
                { 13083, 63 }, // op=0x33 byte=27
                { 13084, 64 }, // op=0x33 byte=28
                { 13085, 24 }, // op=0x33 byte=29
                { 13086, 123 }, // op=0x33 byte=30
                { 13087, 115 }, // op=0x33 byte=31
                { 13088, 51 }, // op=0x33 byte=32
                { 13089, 53 }, // op=0x33 byte=33
                { 13090, 100 }, // op=0x33 byte=34
                { 13091, 37 }, // op=0x33 byte=35
                { 13092, 28 }, // op=0x33 byte=36
                { 13093, 4 }, // op=0x33 byte=37
                { 13094, 13 }, // op=0x33 byte=38
                { 13095, 64 }, // op=0x33 byte=39
                { 13096, 57 }, // op=0x33 byte=40
                { 13097, 381 }, // op=0x33 byte=41
                { 13098, 48 }, // op=0x33 byte=42
                { 13099, 20 }, // op=0x33 byte=43
                { 13100, 60 }, // op=0x33 byte=44
                { 13101, 247 }, // op=0x33 byte=45
                { 13102, 91 }, // op=0x33 byte=46
                { 13103, 52 }, // op=0x33 byte=47
                { 13104, 55 }, // op=0x33 byte=48
                { 13105, 34 }, // op=0x33 byte=49
                { 13106, 61 }, // op=0x33 byte=50
                { 13107, 20 }, // op=0x33 byte=51
                { 13108, 72 }, // op=0x33 byte=52
                { 13109, 60 }, // op=0x33 byte=53
                { 13110, 68 }, // op=0x33 byte=54
                { 13111, 141 }, // op=0x33 byte=55
                { 13112, 106 }, // op=0x33 byte=56
                { 13113, 44 }, // op=0x33 byte=57
                { 13114, 27 }, // op=0x33 byte=58
                { 13115, 81 }, // op=0x33 byte=59
                { 13116, 32 }, // op=0x33 byte=60
                { 13117, 19 }, // op=0x33 byte=61
                { 13118, 23 }, // op=0x33 byte=62
                { 13119, 98 }, // op=0x33 byte=63
                { 13120, 44 }, // op=0x33 byte=64
                { 13121, 32 }, // op=0x33 byte=65
                { 13122, 75 }, // op=0x33 byte=66
                { 13123, 69 }, // op=0x33 byte=67
                { 13124, 61 }, // op=0x33 byte=68
                { 13125, 109 }, // op=0x33 byte=69
                { 13126, 74 }, // op=0x33 byte=70
                { 13127, 240 }, // op=0x33 byte=71
                { 13128, 75 }, // op=0x33 byte=72
                { 13129, 26 }, // op=0x33 byte=73
                { 13130, 3 }, // op=0x33 byte=74
                { 13131, 20 }, // op=0x33 byte=75
                { 13132, 57 }, // op=0x33 byte=76
                { 13133, 40 }, // op=0x33 byte=77
                { 13134, 129 }, // op=0x33 byte=78
                { 13135, 83 }, // op=0x33 byte=79
                { 13136, 82 }, // op=0x33 byte=80
                { 13137, 42 }, // op=0x33 byte=81
                { 13138, 64 }, // op=0x33 byte=82
                { 13139, 43 }, // op=0x33 byte=83
                { 13140, 34 }, // op=0x33 byte=84
                { 13141, 62 }, // op=0x33 byte=85
                { 13142, 93 }, // op=0x33 byte=86
                { 13143, 1 }, // op=0x33 byte=87
                { 13144, 35 }, // op=0x33 byte=88
                { 13145, 33 }, // op=0x33 byte=89
                { 13146, 117 }, // op=0x33 byte=90
                { 13147, 65 }, // op=0x33 byte=91
                { 13148, 113 }, // op=0x33 byte=92
                { 13149, 47 }, // op=0x33 byte=93
                { 13150, 77 }, // op=0x33 byte=94
                { 13151, 39 }, // op=0x33 byte=95
                { 13152, 142 }, // op=0x33 byte=96
                { 13153, 62 }, // op=0x33 byte=97
                { 13154, 14 }, // op=0x33 byte=98
                { 13155, 28 }, // op=0x33 byte=99
                { 13156, 250 }, // op=0x33 byte=100
                { 13157, 15 }, // op=0x33 byte=101
                { 13158, 50 }, // op=0x33 byte=102
                { 13159, 70 }, // op=0x33 byte=103
                { 13160, 55 }, // op=0x33 byte=104
                { 13161, 20 }, // op=0x33 byte=105
                { 13162, 15 }, // op=0x33 byte=106
                { 13163, 40 }, // op=0x33 byte=107
                { 13164, 60 }, // op=0x33 byte=108
                { 13165, 43 }, // op=0x33 byte=109
                { 13166, 47 }, // op=0x33 byte=110
                { 13167, 102 }, // op=0x33 byte=111
                { 13168, 86 }, // op=0x33 byte=112
                { 13169, 84 }, // op=0x33 byte=113
                { 13170, 39 }, // op=0x33 byte=114
                { 13171, 38 }, // op=0x33 byte=115
                { 13172, 106 }, // op=0x33 byte=116
                { 13173, 60 }, // op=0x33 byte=117
                { 13174, 151 }, // op=0x33 byte=118
                { 13175, 231 }, // op=0x33 byte=119
                { 13176, 70 }, // op=0x33 byte=120
                { 13178, 110 }, // op=0x33 byte=122
                { 13179, 92 }, // op=0x33 byte=123
                { 13180, 80 }, // op=0x33 byte=124
                { 13181, 61 }, // op=0x33 byte=125
                { 13182, 61 }, // op=0x33 byte=126
                { 13183, 106 }, // op=0x33 byte=127
                { 13184, 111 }, // op=0x33 byte=128
                { 13185, 5 }, // op=0x33 byte=129
                { 13186, 149 }, // op=0x33 byte=130
                { 13187, 52 }, // op=0x33 byte=131
                { 13188, 94 }, // op=0x33 byte=132
                { 13189, 34 }, // op=0x33 byte=133
                { 13190, 3 }, // op=0x33 byte=134
                { 13191, 61 }, // op=0x33 byte=135
                { 13192, 7 }, // op=0x33 byte=136
                { 13193, 58 }, // op=0x33 byte=137
                { 13194, 45 }, // op=0x33 byte=138
                { 13195, 34 }, // op=0x33 byte=139
                { 13196, 67 }, // op=0x33 byte=140
                { 13197, 35 }, // op=0x33 byte=141
                { 13198, 217 }, // op=0x33 byte=142
                { 13199, 26 }, // op=0x33 byte=143
                { 13200, 64 }, // op=0x33 byte=144
                { 13201, 101 }, // op=0x33 byte=145
                { 13202, 43 }, // op=0x33 byte=146
                { 13203, 11 }, // op=0x33 byte=147
                { 13204, 71 }, // op=0x33 byte=148
                { 13205, 85 }, // op=0x33 byte=149
                { 13206, 103 }, // op=0x33 byte=150
                { 13207, 36 }, // op=0x33 byte=151
                { 13208, 49 }, // op=0x33 byte=152
                { 13209, 230 }, // op=0x33 byte=153
                { 13210, 69 }, // op=0x33 byte=154
                { 13211, 14 }, // op=0x33 byte=155
                { 13212, 19 }, // op=0x33 byte=156
                { 13213, 34 }, // op=0x33 byte=157
                { 13214, 61 }, // op=0x33 byte=158
                { 13215, 70 }, // op=0x33 byte=159
                { 13216, 20 }, // op=0x33 byte=160
                { 13217, 40 }, // op=0x33 byte=161
                { 13218, 13 }, // op=0x33 byte=162
                { 13219, 95 }, // op=0x33 byte=163
                { 13220, 40 }, // op=0x33 byte=164
                { 13221, 77 }, // op=0x33 byte=165
                { 13222, 27 }, // op=0x33 byte=166
                { 13223, 56 }, // op=0x33 byte=167
                { 13224, 51 }, // op=0x33 byte=168
                { 13225, 51 }, // op=0x33 byte=169
                { 13226, 28 }, // op=0x33 byte=170
                { 13227, 267 }, // op=0x33 byte=171
                { 13228, 21 }, // op=0x33 byte=172
                { 13229, 26 }, // op=0x33 byte=173
                { 13230, 68 }, // op=0x33 byte=174
                { 13231, 78 }, // op=0x33 byte=175
                { 13232, 59 }, // op=0x33 byte=176
                { 13233, 39 }, // op=0x33 byte=177
                { 13234, 41 }, // op=0x33 byte=178
                { 13235, 107 }, // op=0x33 byte=179
                { 13236, 186 }, // op=0x33 byte=180
                { 13237, 10 }, // op=0x33 byte=181
                { 13238, 28 }, // op=0x33 byte=182
                { 13239, 32 }, // op=0x33 byte=183
                { 13240, 49 }, // op=0x33 byte=184
                { 13241, 87 }, // op=0x33 byte=185
                { 13242, 62 }, // op=0x33 byte=186
                { 13243, 25 }, // op=0x33 byte=187
                { 13244, 70 }, // op=0x33 byte=188
                { 13245, 34 }, // op=0x33 byte=189
                { 13246, 39 }, // op=0x33 byte=190
                { 13247, 42 }, // op=0x33 byte=191
                { 13248, 41 }, // op=0x33 byte=192
                { 13249, 60 }, // op=0x33 byte=193
                { 13250, 33 }, // op=0x33 byte=194
                { 13251, 101 }, // op=0x33 byte=195
                { 13252, 19 }, // op=0x33 byte=196
                { 13253, 70 }, // op=0x33 byte=197
                { 13254, 39 }, // op=0x33 byte=198
                { 13255, 43 }, // op=0x33 byte=199
                { 13256, 45 }, // op=0x33 byte=200
                { 13257, 46 }, // op=0x33 byte=201
                { 13258, 25 }, // op=0x33 byte=202
                { 13259, 51 }, // op=0x33 byte=203
                { 13260, 56 }, // op=0x33 byte=204
                { 13261, 46 }, // op=0x33 byte=205
                { 13262, 75 }, // op=0x33 byte=206
                { 13263, 43 }, // op=0x33 byte=207
                { 13264, 14 }, // op=0x33 byte=208
                { 13265, 44 }, // op=0x33 byte=209
                { 13266, 17 }, // op=0x33 byte=210
                { 13267, 38 }, // op=0x33 byte=211
                { 13268, 21 }, // op=0x33 byte=212
                { 13269, 33 }, // op=0x33 byte=213
                { 13270, 52 }, // op=0x33 byte=214
                { 13271, 100 }, // op=0x33 byte=215
                { 13272, 69 }, // op=0x33 byte=216
                { 13273, 132 }, // op=0x33 byte=217
                { 13274, 28 }, // op=0x33 byte=218
                { 13275, 51 }, // op=0x33 byte=219
                { 13276, 16 }, // op=0x33 byte=220
                { 13277, 50 }, // op=0x33 byte=221
                { 13278, 6 }, // op=0x33 byte=222
                { 13279, 30 }, // op=0x33 byte=223
                { 13280, 38 }, // op=0x33 byte=224
                { 13281, 38 }, // op=0x33 byte=225
                { 13282, 27 }, // op=0x33 byte=226
                { 13283, 165 }, // op=0x33 byte=227
                { 13284, 40 }, // op=0x33 byte=228
                { 13285, 43 }, // op=0x33 byte=229
                { 13286, 30 }, // op=0x33 byte=230
                { 13287, 61 }, // op=0x33 byte=231
                { 13288, 125 }, // op=0x33 byte=232
                { 13289, 86 }, // op=0x33 byte=233
                { 13290, 68 }, // op=0x33 byte=234
                { 13291, 34 }, // op=0x33 byte=235
                { 13292, 187 }, // op=0x33 byte=236
                { 13293, 40 }, // op=0x33 byte=237
                { 13294, 9 }, // op=0x33 byte=238
                { 13295, 148 }, // op=0x33 byte=239
                { 13296, 24 }, // op=0x33 byte=240
                { 13297, 27 }, // op=0x33 byte=241
                { 13298, 36 }, // op=0x33 byte=242
                { 13299, 46 }, // op=0x33 byte=243
                { 13300, 100 }, // op=0x33 byte=244
                { 13301, 47 }, // op=0x33 byte=245
                { 13302, 28 }, // op=0x33 byte=246
                { 13303, 87 }, // op=0x33 byte=247
                { 13304, 58 }, // op=0x33 byte=248
                { 13305, 89 }, // op=0x33 byte=249
                { 13306, 14 }, // op=0x33 byte=250
                { 13307, 99 }, // op=0x33 byte=251
                { 13308, 41 }, // op=0x33 byte=252
                { 13309, 92 }, // op=0x33 byte=253
                { 13310, 70 }, // op=0x33 byte=254
                { 13311, 6 }, // op=0x33 byte=255
                { 65536, 388 }, // op=0x100 byte=0
                { 65537, 12 }, // op=0x100 byte=1
                { 65538, 2 }, // op=0x100 byte=2
                { 65551, 3 }, // op=0x100 byte=15
                { 65569, 3 }, // op=0x100 byte=33
                { 65791, 272 }, // op=0x100 byte=255
                { 66304, 200 }, // op=0x103 byte=0
                { 66305, 1 }, // op=0x103 byte=1
                { 66307, 21 }, // op=0x103 byte=3
                { 66311, 1 }, // op=0x103 byte=7
                { 66320, 8 }, // op=0x103 byte=16
                { 66323, 1 }, // op=0x103 byte=19
                { 66324, 1 }, // op=0x103 byte=20
                { 66343, 8 }, // op=0x103 byte=39
                { 66440, 1 }, // op=0x103 byte=136
                { 66512, 1 }, // op=0x103 byte=208
                { 66536, 21 }, // op=0x103 byte=232
                { 66560, 2512 }, // op=0x104 byte=0
                { 66561, 81 }, // op=0x104 byte=1
                { 66562, 37 }, // op=0x104 byte=2
                { 66563, 41 }, // op=0x104 byte=3
                { 66564, 19 }, // op=0x104 byte=4
                { 66565, 19 }, // op=0x104 byte=5
                { 66566, 6 }, // op=0x104 byte=6
                { 66567, 6 }, // op=0x104 byte=7
                { 66568, 4 }, // op=0x104 byte=8
                { 66569, 4 }, // op=0x104 byte=9
                { 66570, 2 }, // op=0x104 byte=10
                { 66571, 1 }, // op=0x104 byte=11
                { 66572, 1 }, // op=0x104 byte=12
                { 66573, 1 }, // op=0x104 byte=13
                { 66574, 3 }, // op=0x104 byte=14
                { 66575, 2 }, // op=0x104 byte=15
                { 66596, 13 }, // op=0x104 byte=36
                { 66815, 1944 }, // op=0x104 byte=255
                { 66816, 72 }, // op=0x105 byte=0
                { 67042, 12 }, // op=0x105 byte=226
                { 67071, 12 }, // op=0x105 byte=255
                { 68095, 1952 }, // op=0x109 byte=255
                { 69631, 8072 }, // op=0x10f byte=255
                { 69632, 136 }, // op=0x110 byte=0
                { 69887, 5464 }, // op=0x110 byte=255
                { 69888, 7438 }, // op=0x111 byte=0
                { 69889, 63 }, // op=0x111 byte=1
                { 69891, 3 }, // op=0x111 byte=3
                { 69893, 16 }, // op=0x111 byte=5
                { 69898, 43 }, // op=0x111 byte=10
                { 69903, 25 }, // op=0x111 byte=15
                { 69904, 6 }, // op=0x111 byte=16
                { 69908, 217 }, // op=0x111 byte=20
                { 69913, 16 }, // op=0x111 byte=25
                { 69918, 19 }, // op=0x111 byte=30
                { 69927, 6 }, // op=0x111 byte=39
                { 69932, 13 }, // op=0x111 byte=44
                { 69938, 290 }, // op=0x111 byte=50
                { 69948, 12 }, // op=0x111 byte=60
                { 69958, 2 }, // op=0x111 byte=70
                { 69982, 23 }, // op=0x111 byte=94
                { 69988, 21 }, // op=0x111 byte=100
                { 70038, 22 }, // op=0x111 byte=150
                { 70088, 10 }, // op=0x111 byte=200
                { 70132, 25 }, // op=0x111 byte=244
                { 70138, 2 }, // op=0x111 byte=250
                { 70143, 760 }, // op=0x111 byte=255
                { 70400, 1125 }, // op=0x113 byte=0
                { 70401, 2 }, // op=0x113 byte=1
                { 70405, 4 }, // op=0x113 byte=5
                { 70415, 2 }, // op=0x113 byte=15
                { 70425, 7 }, // op=0x113 byte=25
                { 70430, 4 }, // op=0x113 byte=30
                { 70440, 1 }, // op=0x113 byte=40
                { 70450, 2 }, // op=0x113 byte=50
                { 70475, 1 }, // op=0x113 byte=75
                { 70500, 28 }, // op=0x113 byte=100
                { 70655, 288 }, // op=0x113 byte=255
                { 70912, 1913 }, // op=0x115 byte=0
                { 70913, 64 }, // op=0x115 byte=1
                { 70914, 42 }, // op=0x115 byte=2
                { 70915, 39 }, // op=0x115 byte=3
                { 70916, 18 }, // op=0x115 byte=4
                { 70917, 6 }, // op=0x115 byte=5
                { 70948, 4 }, // op=0x115 byte=36
                { 70949, 4 }, // op=0x115 byte=37
                { 70950, 4 }, // op=0x115 byte=38
                { 70951, 2 }, // op=0x115 byte=39
                { 71167, 968 }, // op=0x115 byte=255
                { 71423, 992 }, // op=0x116 byte=255
                { 71424, 1518 }, // op=0x117 byte=0
                { 71425, 2 }, // op=0x117 byte=1
                { 71426, 1 }, // op=0x117 byte=2
                { 71434, 2 }, // op=0x117 byte=10
                { 71438, 1 }, // op=0x117 byte=14
                { 71439, 7 }, // op=0x117 byte=15
                { 71444, 2 }, // op=0x117 byte=20
                { 71449, 1 }, // op=0x117 byte=25
                { 71454, 103 }, // op=0x117 byte=30
                { 71468, 2 }, // op=0x117 byte=44
                { 71474, 26 }, // op=0x117 byte=50
                { 71484, 6 }, // op=0x117 byte=60
                { 71574, 5 }, // op=0x117 byte=150
                { 71624, 4 }, // op=0x117 byte=200
                { 71679, 192 }, // op=0x117 byte=255
                { 71680, 39535 }, // op=0x118 byte=0
                { 71681, 877 }, // op=0x118 byte=1
                { 71682, 255 }, // op=0x118 byte=2
                { 71683, 1267 }, // op=0x118 byte=3
                { 71684, 42 }, // op=0x118 byte=4
                { 71685, 402 }, // op=0x118 byte=5
                { 71686, 16 }, // op=0x118 byte=6
                { 71687, 121 }, // op=0x118 byte=7
                { 71688, 85 }, // op=0x118 byte=8
                { 71689, 12 }, // op=0x118 byte=9
                { 71690, 357 }, // op=0x118 byte=10
                { 71692, 24 }, // op=0x118 byte=12
                { 71694, 8 }, // op=0x118 byte=14
                { 71695, 31 }, // op=0x118 byte=15
                { 71696, 213 }, // op=0x118 byte=16
                { 71698, 1 }, // op=0x118 byte=18
                { 71699, 11 }, // op=0x118 byte=19
                { 71700, 300 }, // op=0x118 byte=20
                { 71702, 2 }, // op=0x118 byte=22
                { 71703, 5 }, // op=0x118 byte=23
                { 71704, 2 }, // op=0x118 byte=24
                { 71705, 50 }, // op=0x118 byte=25
                { 71710, 316 }, // op=0x118 byte=30
                { 71711, 7 }, // op=0x118 byte=31
                { 71712, 798 }, // op=0x118 byte=32
                { 71713, 1 }, // op=0x118 byte=33
                { 71715, 2 }, // op=0x118 byte=35
                { 71716, 2 }, // op=0x118 byte=36
                { 71719, 206 }, // op=0x118 byte=39
                { 71720, 40 }, // op=0x118 byte=40
                { 71722, 5 }, // op=0x118 byte=42
                { 71724, 61 }, // op=0x118 byte=44
                { 71725, 4 }, // op=0x118 byte=45
                { 71726, 2 }, // op=0x118 byte=46
                { 71728, 8 }, // op=0x118 byte=48
                { 71730, 118 }, // op=0x118 byte=50
                { 71740, 99 }, // op=0x118 byte=60
                { 71744, 1 }, // op=0x118 byte=64
                { 71745, 1 }, // op=0x118 byte=65
                { 71750, 1 }, // op=0x118 byte=70
                { 71755, 5 }, // op=0x118 byte=75
                { 71758, 797 }, // op=0x118 byte=78
                { 71760, 3 }, // op=0x118 byte=80
                { 71763, 1 }, // op=0x118 byte=83
                { 71764, 11 }, // op=0x118 byte=84
                { 71765, 23 }, // op=0x118 byte=85
                { 71768, 19 }, // op=0x118 byte=88
                { 71770, 10 }, // op=0x118 byte=90
                { 71774, 2 }, // op=0x118 byte=94
                { 71776, 11 }, // op=0x118 byte=96
                { 71777, 3 }, // op=0x118 byte=97
                { 71780, 170 }, // op=0x118 byte=100
                { 71797, 6 }, // op=0x118 byte=117
                { 71800, 12 }, // op=0x118 byte=120
                { 71801, 1 }, // op=0x118 byte=121
                { 71805, 9 }, // op=0x118 byte=125
                { 71810, 18 }, // op=0x118 byte=130
                { 71812, 4 }, // op=0x118 byte=132
                { 71816, 11 }, // op=0x118 byte=136
                { 71820, 2 }, // op=0x118 byte=140
                { 71824, 6 }, // op=0x118 byte=144
                { 71830, 63 }, // op=0x118 byte=150
                { 71840, 3 }, // op=0x118 byte=160
                { 71848, 3 }, // op=0x118 byte=168
                { 71856, 3 }, // op=0x118 byte=176
                { 71872, 1 }, // op=0x118 byte=192
                { 71876, 5 }, // op=0x118 byte=196
                { 71880, 111 }, // op=0x118 byte=200
                { 71888, 29 }, // op=0x118 byte=208
                { 71900, 1 }, // op=0x118 byte=220
                { 71904, 2 }, // op=0x118 byte=224
                { 71905, 1 }, // op=0x118 byte=225
                { 71912, 117 }, // op=0x118 byte=232
                { 71920, 22 }, // op=0x118 byte=240
                { 71924, 36 }, // op=0x118 byte=244
                { 71930, 7 }, // op=0x118 byte=250
                { 71935, 1912 }, // op=0x118 byte=255
                { 71936, 19113 }, // op=0x119 byte=0
                { 71937, 466 }, // op=0x119 byte=1
                { 71938, 326 }, // op=0x119 byte=2
                { 71939, 6 }, // op=0x119 byte=3
                { 71941, 10 }, // op=0x119 byte=5
                { 71946, 56 }, // op=0x119 byte=10
                { 71952, 10 }, // op=0x119 byte=16
                { 71956, 27 }, // op=0x119 byte=20
                { 71966, 43 }, // op=0x119 byte=30
                { 71968, 12 }, // op=0x119 byte=32
                { 71975, 10 }, // op=0x119 byte=39
                { 71980, 2 }, // op=0x119 byte=44
                { 71984, 1 }, // op=0x119 byte=48
                { 71986, 2 }, // op=0x119 byte=50
                { 71996, 15 }, // op=0x119 byte=60
                { 72014, 12 }, // op=0x119 byte=78
                { 72036, 1 }, // op=0x119 byte=100
                { 72053, 1 }, // op=0x119 byte=117
                { 72086, 2 }, // op=0x119 byte=150
                { 72136, 3 }, // op=0x119 byte=200
                { 72168, 1 }, // op=0x119 byte=232
                { 72186, 1 }, // op=0x119 byte=250
                { 72191, 944 }, // op=0x119 byte=255
                { 72192, 511 }, // op=0x11a byte=0
                { 72202, 1 }, // op=0x11a byte=10
                { 72204, 12 }, // op=0x11a byte=12
                { 72212, 27 }, // op=0x11a byte=20
                { 72217, 11 }, // op=0x11a byte=25
                { 72222, 13 }, // op=0x11a byte=30
                { 72232, 3 }, // op=0x11a byte=40
                { 72237, 6 }, // op=0x11a byte=45
                { 72447, 120 }, // op=0x11a byte=255
                { 72448, 170 }, // op=0x11b byte=0
                { 72449, 52 }, // op=0x11b byte=1
                { 72450, 24 }, // op=0x11b byte=2
                { 72451, 13 }, // op=0x11b byte=3
                { 72492, 20 }, // op=0x11b byte=44
                { 72592, 11 }, // op=0x11b byte=144
                { 72648, 6 }, // op=0x11b byte=200
                { 72959, 88 }, // op=0x11c byte=255
                { 73215, 824 }, // op=0x11d byte=255
                { 73471, 840 }, // op=0x11e byte=255
                { 73727, 1288 }, // op=0x11f byte=255
                { 73983, 2176 }, // op=0x120 byte=255
                { 74239, 8 }, // op=0x121 byte=255
                { 74240, 358 }, // op=0x122 byte=0
                { 74250, 40 }, // op=0x122 byte=10
                { 74260, 10 }, // op=0x122 byte=20
                { 74751, 104 }, // op=0x123 byte=255
                { 74752, 70 }, // op=0x124 byte=0
                { 74753, 1 }, // op=0x124 byte=1
                { 74754, 2 }, // op=0x124 byte=2
                { 74757, 7 }, // op=0x124 byte=5
                { 75007, 136 }, // op=0x124 byte=255
                { 75008, 70 }, // op=0x125 byte=0
                { 75015, 6 }, // op=0x125 byte=7
                { 75028, 4 }, // op=0x125 byte=20
                { 75263, 48 }, // op=0x125 byte=255
                { 75519, 80 }, // op=0x126 byte=255
                { 75775, 2200 }, // op=0x127 byte=255
                { 76031, 16 }, // op=0x128 byte=255
                { 76287, 240 }, // op=0x129 byte=255
                { 76544, 36 }, // op=0x12b byte=0
                { 76549, 6 }, // op=0x12b byte=5
                { 76574, 3 }, // op=0x12b byte=30
                { 76584, 3 }, // op=0x12b byte=40
                { 76800, 24 }, // op=0x12c byte=0
                { 76810, 4 }, // op=0x12c byte=10
                { 76850, 4 }, // op=0x12c byte=50
                { 77055, 96 }, // op=0x12c byte=255
                { 77311, 848 }, // op=0x12d byte=255
                { 77312, 14 }, // op=0x12e byte=0
                { 77313, 2 }, // op=0x12e byte=1
                { 77567, 832 }, // op=0x12e byte=255
                { 77823, 688 }, // op=0x12f byte=255
                { 78079, 40 }, // op=0x130 byte=255
                { 78591, 456 }, // op=0x132 byte=255
                { 78592, 154 }, // op=0x133 byte=0
                { 78593, 20 }, // op=0x133 byte=1
                { 78594, 2 }, // op=0x133 byte=2
                { 78847, 232 }, // op=0x133 byte=255
                { 78848, 42 }, // op=0x134 byte=0
                { 78849, 6 }, // op=0x134 byte=1
                { 79103, 576 }, // op=0x134 byte=255
                { 79359, 520 }, // op=0x135 byte=255
                { 79615, 216 }, // op=0x136 byte=255
                { 79871, 16 }, // op=0x137 byte=255
                { 79872, 16 }, // op=0x138 byte=0
                { 80127, 24 }, // op=0x138 byte=255
                { 80383, 16 }, // op=0x139 byte=255
                { 80639, 24 }, // op=0x13a byte=255
                { 81151, 24 }, // op=0x13c byte=255
                { 81407, 8 }, // op=0x13d byte=255
                { 81663, 8 }, // op=0x13e byte=255
                { 81919, 696 }, // op=0x13f byte=255
                { 82175, 1544 }, // op=0x140 byte=255
                { 82431, 4936 }, // op=0x141 byte=255
                { 82687, 64 }, // op=0x142 byte=255
                { 82943, 136 }, // op=0x143 byte=255
                { 83199, 512 }, // op=0x144 byte=255
                { 83455, 120 }, // op=0x145 byte=255
                { 83711, 8 }, // op=0x146 byte=255
                { 83967, 544 }, // op=0x147 byte=255
                { 84223, 8 }, // op=0x148 byte=255
                { 84479, 16 }, // op=0x149 byte=255
                { 84735, 16 }, // op=0x14a byte=255
                { 84991, 8 }, // op=0x14b byte=255
                { 84992, 855 }, // op=0x14c byte=0
                { 84993, 24 }, // op=0x14c byte=1
                { 84994, 17 }, // op=0x14c byte=2
                { 84995, 9 }, // op=0x14c byte=3
                { 84996, 6 }, // op=0x14c byte=4
                { 84997, 1 }, // op=0x14c byte=5
                { 84999, 1 }, // op=0x14c byte=7
                { 85001, 3 }, // op=0x14c byte=9
                { 85002, 1 }, // op=0x14c byte=10
                { 85007, 3 }, // op=0x14c byte=15
                { 85247, 24 }, // op=0x14c byte=255
                { 85503, 1200 }, // op=0x14d byte=255
                { 85504, 86 }, // op=0x14e byte=0
                { 85505, 11 }, // op=0x14e byte=1
                { 85506, 2 }, // op=0x14e byte=2
                { 85509, 4 }, // op=0x14e byte=5
                { 85519, 1 }, // op=0x14e byte=15
                { 85759, 576 }, // op=0x14e byte=255
                { 85760, 354 }, // op=0x14f byte=0
                { 85765, 23 }, // op=0x14f byte=5
                { 85770, 23 }, // op=0x14f byte=10
                { 85780, 2 }, // op=0x14f byte=20
                { 86011, 3 }, // op=0x14f byte=251
                { 86015, 3 }, // op=0x14f byte=255
                { 86271, 104 }, // op=0x150 byte=255
                { 86783, 8 }, // op=0x152 byte=255
                { 86784, 104 }, // op=0x153 byte=0
                { 87039, 128 }, // op=0x153 byte=255
                { 87040, 384 }, // op=0x154 byte=0
                { 87295, 256 }, // op=0x154 byte=255
                { 87552, 116 }, // op=0x156 byte=0
                { 87553, 12 }, // op=0x156 byte=1
                { 87807, 16 }, // op=0x156 byte=255
                { 87808, 49 }, // op=0x157 byte=0
                { 87811, 7 }, // op=0x157 byte=3
                { 88063, 424 }, // op=0x157 byte=255
                { 88320, 18 }, // op=0x159 byte=0
                { 88321, 6 }, // op=0x159 byte=1
                { 88831, 128 }, // op=0x15a byte=255
                { 88832, 517 }, // op=0x15b byte=0
                { 88833, 6 }, // op=0x15b byte=1
                { 88834, 2 }, // op=0x15b byte=2
                { 88835, 41 }, // op=0x15b byte=3
                { 88856, 24 }, // op=0x15b byte=24
                { 88862, 4 }, // op=0x15b byte=30
                { 88864, 8 }, // op=0x15b byte=32
                { 88876, 2 }, // op=0x15b byte=44
                { 88882, 6 }, // op=0x15b byte=50
                { 88976, 1 }, // op=0x15b byte=144
                { 89020, 2 }, // op=0x15b byte=188
                { 89064, 33 }, // op=0x15b byte=232
                { 89076, 2 }, // op=0x15b byte=244
                { 89084, 24 }, // op=0x15b byte=252
                { 89087, 152 }, // op=0x15b byte=255
                { 89088, 154 }, // op=0x15c byte=0
                { 89294, 15 }, // op=0x15c byte=206
                { 89343, 231 }, // op=0x15c byte=255
                { 89344, 55 }, // op=0x15d byte=0
                { 89345, 1 }, // op=0x15d byte=1
                { 89444, 7 }, // op=0x15d byte=100
                { 89588, 1 }, // op=0x15d byte=244
                { 89599, 272 }, // op=0x15d byte=255
                { 89600, 56 }, // op=0x15e byte=0
                { 89855, 304 }, // op=0x15e byte=255
                { 89856, 69 }, // op=0x15f byte=0
                { 89857, 1 }, // op=0x15f byte=1
                { 89886, 2 }, // op=0x15f byte=30
                { 89956, 7 }, // op=0x15f byte=100
                { 90100, 1 }, // op=0x15f byte=244
                { 90111, 216 }, // op=0x15f byte=255
                { 90879, 88 }, // op=0x162 byte=255
                { 91135, 2024 }, // op=0x163 byte=255
                { 91391, 256 }, // op=0x164 byte=255
                { 91647, 672 }, // op=0x165 byte=255
                { 91903, 128 }, // op=0x166 byte=255
                { 92159, 120 }, // op=0x167 byte=255
                { 92415, 96 }, // op=0x168 byte=255
                { 92671, 224 }, // op=0x169 byte=255
                { 92927, 384 }, // op=0x16a byte=255
                { 93183, 3512 }, // op=0x16b byte=255
                { 93439, 360 }, // op=0x16c byte=255
                { 93695, 1368 }, // op=0x16d byte=255
                { 93696, 1281 }, // op=0x16e byte=0
                { 93697, 126 }, // op=0x16e byte=1
                { 93698, 5 }, // op=0x16e byte=2
                { 93701, 1 }, // op=0x16e byte=5
                { 93702, 3 }, // op=0x16e byte=6
                { 93703, 1 }, // op=0x16e byte=7
                { 93704, 4 }, // op=0x16e byte=8
                { 93706, 1 }, // op=0x16e byte=10
                { 93708, 33 }, // op=0x16e byte=12
                { 93720, 9 }, // op=0x16e byte=24
                { 93951, 120 }, // op=0x16e byte=255
                { 93952, 203 }, // op=0x16f byte=0
                { 93953, 11 }, // op=0x16f byte=1
                { 93955, 2 }, // op=0x16f byte=3
                { 93957, 5 }, // op=0x16f byte=5
                { 93962, 5 }, // op=0x16f byte=10
                { 93967, 3 }, // op=0x16f byte=15
                { 94012, 3 }, // op=0x16f byte=60
                { 94207, 48 }, // op=0x16f byte=255
                { 94463, 576 }, // op=0x170 byte=255
                { 94719, 120 }, // op=0x171 byte=255
                { 94720, 2611 }, // op=0x172 byte=0
                { 94721, 9 }, // op=0x172 byte=1
                { 94722, 3 }, // op=0x172 byte=2
                { 94725, 10 }, // op=0x172 byte=5
                { 94730, 47 }, // op=0x172 byte=10
                { 94740, 88 }, // op=0x172 byte=20
                { 94745, 6 }, // op=0x172 byte=25
                { 94750, 54 }, // op=0x172 byte=30
                { 94755, 1 }, // op=0x172 byte=35
                { 94765, 2 }, // op=0x172 byte=45
                { 94770, 19 }, // op=0x172 byte=50
                { 94780, 9 }, // op=0x172 byte=60
                { 94810, 1 }, // op=0x172 byte=90
                { 94820, 3 }, // op=0x172 byte=100
                { 94840, 3 }, // op=0x172 byte=120
                { 94864, 5 }, // op=0x172 byte=144
                { 94870, 15 }, // op=0x172 byte=150
                { 94920, 2 }, // op=0x172 byte=200
                { 94975, 72 }, // op=0x172 byte=255
                { 95231, 152 }, // op=0x173 byte=255
                { 95487, 152 }, // op=0x174 byte=255
                { 95743, 128 }, // op=0x175 byte=255
                { 95999, 272 }, // op=0x176 byte=255
                { 96255, 176 }, // op=0x177 byte=255
                { 96256, 224 }, // op=0x178 byte=0
                { 96511, 24 }, // op=0x178 byte=255
                { 96512, 104 }, // op=0x179 byte=0
                { 96767, 40 }, // op=0x179 byte=255
                { 96768, 40 }, // op=0x17a byte=0
                { 97279, 352 }, // op=0x17b byte=255
                { 97535, 64 }, // op=0x17c byte=255
                { 97791, 112 }, // op=0x17d byte=255
                { 98047, 928 }, // op=0x17e byte=255
                { 98303, 192 }, // op=0x17f byte=255
                { 98559, 192 }, // op=0x180 byte=255
                { 98815, 176 }, // op=0x181 byte=255
                { 99071, 368 }, // op=0x182 byte=255
                { 99327, 64 }, // op=0x183 byte=255
                { 99583, 120 }, // op=0x184 byte=255
                { 99584, 251 }, // op=0x185 byte=0
                { 99585, 29 }, // op=0x185 byte=1
                { 100095, 48 }, // op=0x186 byte=255
                { 100351, 112 }, // op=0x187 byte=255
                { 100607, 56 }, // op=0x188 byte=255
                { 100863, 160 }, // op=0x189 byte=255
                { 101119, 264 }, // op=0x18a byte=255
                { 101375, 40 }, // op=0x18b byte=255
                { 101631, 24 }, // op=0x18c byte=255
                { 101887, 3584 }, // op=0x18d byte=255
                { 102143, 392 }, // op=0x18e byte=255
                { 102144, 356 }, // op=0x18f byte=0
                { 102145, 68 }, // op=0x18f byte=1
                { 102399, 128 }, // op=0x18f byte=255
                { 102400, 444 }, // op=0x190 byte=0
                { 102401, 17 }, // op=0x190 byte=1
                { 102402, 21 }, // op=0x190 byte=2
                { 102403, 6 }, // op=0x190 byte=3
                { 102655, 16 }, // op=0x190 byte=255
                { 102656, 894 }, // op=0x191 byte=0
                { 102658, 7 }, // op=0x191 byte=2
                { 102659, 13 }, // op=0x191 byte=3
                { 102660, 1 }, // op=0x191 byte=4
                { 102661, 1 }, // op=0x191 byte=5
                { 102662, 6 }, // op=0x191 byte=6
                { 102663, 6 }, // op=0x191 byte=7
                { 102665, 32 }, // op=0x191 byte=9
                { 102666, 12 }, // op=0x191 byte=10
                { 102668, 10 }, // op=0x191 byte=12
                { 102669, 12 }, // op=0x191 byte=13
                { 102670, 4 }, // op=0x191 byte=14
                { 102672, 8 }, // op=0x191 byte=16
                { 102673, 8 }, // op=0x191 byte=17
                { 102676, 8 }, // op=0x191 byte=20
                { 102677, 6 }, // op=0x191 byte=21
                { 102682, 4 }, // op=0x191 byte=26
                { 102685, 7 }, // op=0x191 byte=29
                { 102686, 2 }, // op=0x191 byte=30
                { 102687, 13 }, // op=0x191 byte=31
                { 102688, 2 }, // op=0x191 byte=32
                { 102689, 2 }, // op=0x191 byte=33
                { 102696, 3 }, // op=0x191 byte=40
                { 102697, 3 }, // op=0x191 byte=41
                { 102703, 8 }, // op=0x191 byte=47
                { 102705, 8 }, // op=0x191 byte=49
                { 102709, 8 }, // op=0x191 byte=53
                { 102711, 8 }, // op=0x191 byte=55
                { 102719, 8 }, // op=0x191 byte=63
                { 102723, 8 }, // op=0x191 byte=67
                { 102724, 8 }, // op=0x191 byte=68
                { 102728, 6 }, // op=0x191 byte=72
                { 102730, 18 }, // op=0x191 byte=74
                { 102735, 6 }, // op=0x191 byte=79
                { 102737, 12 }, // op=0x191 byte=81
                { 102738, 6 }, // op=0x191 byte=82
                { 102749, 4 }, // op=0x191 byte=93
                { 102750, 4 }, // op=0x191 byte=94
                { 102911, 256 }, // op=0x191 byte=255
                { 102912, 24 }, // op=0x192 byte=0
                { 103167, 728 }, // op=0x192 byte=255
                { 103423, 1544 }, // op=0x193 byte=255
                { 103679, 16 }, // op=0x194 byte=255
                { 103680, 35 }, // op=0x195 byte=0
                { 103780, 4 }, // op=0x195 byte=100
                { 103781, 1 }, // op=0x195 byte=101
                { 103935, 776 }, // op=0x195 byte=255
                { 104191, 880 }, // op=0x196 byte=255
                { 104192, 72 }, // op=0x197 byte=0
                { 104447, 448 }, // op=0x197 byte=255
                { 104703, 440 }, // op=0x198 byte=255
                { 104959, 136 }, // op=0x199 byte=255
                { 105215, 432 }, // op=0x19a byte=255
                { 105471, 72 }, // op=0x19b byte=255
                { 105472, 104 }, // op=0x19c byte=0
                { 105727, 32 }, // op=0x19c byte=255
                { 105728, 72 }, // op=0x19d byte=0
                { 105729, 24 }, // op=0x19d byte=1
                { 106239, 16 }, // op=0x19e byte=255
                { 106240, 16 }, // op=0x19f byte=0
                { 106495, 344 }, // op=0x19f byte=255
                { 106751, 312 }, // op=0x1a0 byte=255
                { 106752, 152 }, // op=0x1a1 byte=0
                { 107007, 216 }, // op=0x1a1 byte=255
                { 107264, 176 }, // op=0x1a3 byte=0
                { 107265, 19 }, // op=0x1a3 byte=1
                { 107266, 5 }, // op=0x1a3 byte=2
                { 107519, 64 }, // op=0x1a3 byte=255
                { 107775, 8 }, // op=0x1a4 byte=255
                { 108031, 368 }, // op=0x1a5 byte=255
                { 108287, 64 }, // op=0x1a6 byte=255
                { 108543, 208 }, // op=0x1a7 byte=255
                { 108799, 792 }, // op=0x1a8 byte=255
                { 108800, 230 }, // op=0x1a9 byte=0
                { 108802, 1 }, // op=0x1a9 byte=2
                { 108803, 1 }, // op=0x1a9 byte=3
                { 108810, 2 }, // op=0x1a9 byte=10
                { 108820, 16 }, // op=0x1a9 byte=20
                { 108830, 1 }, // op=0x1a9 byte=30
                { 108835, 2 }, // op=0x1a9 byte=35
                { 108850, 1 }, // op=0x1a9 byte=50
                { 108900, 3 }, // op=0x1a9 byte=100
                { 108950, 6 }, // op=0x1a9 byte=150
                { 109032, 1 }, // op=0x1a9 byte=232
                { 109055, 8 }, // op=0x1a9 byte=255
                { 109311, 40 }, // op=0x1aa byte=255
                { 109567, 48 }, // op=0x1ab byte=255
                { 109823, 184 }, // op=0x1ac byte=255
                { 110079, 112 }, // op=0x1ad byte=255
                { 110335, 56 }, // op=0x1ae byte=255
                { 110336, 32 }, // op=0x1af byte=0
                { 110591, 8 }, // op=0x1af byte=255
                { 110847, 160 }, // op=0x1b0 byte=255
                { 111103, 152 }, // op=0x1b1 byte=255
                { 111359, 72 }, // op=0x1b2 byte=255
                { 111360, 7 }, // op=0x1b3 byte=0
                { 111560, 1 }, // op=0x1b3 byte=200
                { 111615, 80 }, // op=0x1b3 byte=255
                { 111871, 8 }, // op=0x1b4 byte=255
                { 112127, 16 }, // op=0x1b5 byte=255
                { 112383, 432 }, // op=0x1b6 byte=255
                { 112639, 136 }, // op=0x1b7 byte=255
                { 112640, 7 }, // op=0x1b8 byte=0
                { 112665, 1 }, // op=0x1b8 byte=25
                { 113151, 8 }, // op=0x1b9 byte=255
                { 113407, 8 }, // op=0x1ba byte=255
                { 113663, 40 }, // op=0x1bb byte=255
                { 113664, 49 }, // op=0x1bc byte=0
                { 113666, 3 }, // op=0x1bc byte=2
                { 113667, 4 }, // op=0x1bc byte=3
                { 114175, 72 }, // op=0x1bd byte=255
                { 114431, 712 }, // op=0x1be byte=255
                { 114432, 36 }, // op=0x1bf byte=0
                { 114434, 2 }, // op=0x1bf byte=2
                { 114435, 3 }, // op=0x1bf byte=3
                { 114436, 1 }, // op=0x1bf byte=4
                { 114441, 2 }, // op=0x1bf byte=9
                { 114457, 4 }, // op=0x1bf byte=25
                { 114687, 8 }, // op=0x1bf byte=255
                { 114943, 56 }, // op=0x1c0 byte=255
                { 115199, 32 }, // op=0x1c1 byte=255
                { 115711, 72 }, // op=0x1c3 byte=255
                { 115967, 344 }, // op=0x1c4 byte=255
                { 116223, 40 }, // op=0x1c5 byte=255
                { 116224, 35 }, // op=0x1c6 byte=0
                { 116236, 1 }, // op=0x1c6 byte=12
                { 116238, 1 }, // op=0x1c6 byte=14
                { 116240, 1 }, // op=0x1c6 byte=16
                { 116242, 1 }, // op=0x1c6 byte=18
                { 116250, 1 }, // op=0x1c6 byte=26
                { 116735, 8 }, // op=0x1c7 byte=255
                { 116736, 49 }, // op=0x1c8 byte=0
                { 116737, 1 }, // op=0x1c8 byte=1
                { 116738, 2 }, // op=0x1c8 byte=2
                { 116741, 2 }, // op=0x1c8 byte=5
                { 116746, 2 }, // op=0x1c8 byte=10
                { 116992, 28 }, // op=0x1c9 byte=0
                { 116993, 2 }, // op=0x1c9 byte=1
                { 117002, 2 }, // op=0x1c9 byte=10
                { 117247, 8 }, // op=0x1c9 byte=255
                { 117503, 16 }, // op=0x1ca byte=255
                { 117759, 16 }, // op=0x1cb byte=255
                { 118015, 104 }, // op=0x1cc byte=255
                { 118271, 88 }, // op=0x1cd byte=255
                { 118527, 16 }, // op=0x1ce byte=255
                { 118528, 53 }, // op=0x1cf byte=0
                { 118529, 7 }, // op=0x1cf byte=1
                { 118531, 2 }, // op=0x1cf byte=3
                { 118533, 2 }, // op=0x1cf byte=5
                { 118783, 104 }, // op=0x1cf byte=255
                { 119039, 112 }, // op=0x1d0 byte=255
                { 119295, 16 }, // op=0x1d1 byte=255
                { 119807, 1248 }, // op=0x1d3 byte=255
                { 120063, 928 }, // op=0x1d4 byte=255
                { 120319, 536 }, // op=0x1d5 byte=255
                { 120575, 56 }, // op=0x1d6 byte=255
                { 121087, 56 }, // op=0x1d8 byte=255
                { 121343, 8 }, // op=0x1d9 byte=255
                { 121599, 8 }, // op=0x1da byte=255
                { 121855, 8 }, // op=0x1db byte=255
                { 122111, 40 }, // op=0x1dc byte=255
                { 122367, 72 }, // op=0x1dd byte=255
                { 122879, 680 }, // op=0x1df byte=255
                { 123135, 40 }, // op=0x1e0 byte=255
                { 123391, 40 }, // op=0x1e1 byte=255
                { 123392, 32 }, // op=0x1e2 byte=0
                { 123648, 14 }, // op=0x1e3 byte=0
                { 123649, 2 }, // op=0x1e3 byte=1
                { 123903, 576 }, // op=0x1e3 byte=255
                { 124159, 56 }, // op=0x1e4 byte=255
                { 124415, 160 }, // op=0x1e5 byte=255
                { 124416, 15 }, // op=0x1e6 byte=0
                { 124417, 1 }, // op=0x1e6 byte=1
                { 124671, 96 }, // op=0x1e6 byte=255
                { 124927, 8 }, // op=0x1e7 byte=255
                { 125183, 104 }, // op=0x1e8 byte=255
                { 125439, 80 }, // op=0x1e9 byte=255
                { 125440, 105 }, // op=0x1ea byte=0
                { 125441, 7 }, // op=0x1ea byte=1
                { 125695, 8 }, // op=0x1ea byte=255
                { 125951, 16 }, // op=0x1eb byte=255
                { 126207, 512 }, // op=0x1ec byte=255
                { 126463, 64 }, // op=0x1ed byte=255
                { 126719, 40 }, // op=0x1ee byte=255
                { 126720, 28 }, // op=0x1ef byte=0
                { 126728, 2 }, // op=0x1ef byte=8
                { 126740, 2 }, // op=0x1ef byte=20
                { 126975, 80 }, // op=0x1ef byte=255
                { 126976, 56 }, // op=0x1f0 byte=0
                { 126984, 4 }, // op=0x1f0 byte=8
                { 126996, 4 }, // op=0x1f0 byte=20
                { 127231, 40 }, // op=0x1f0 byte=255
                { 127487, 184 }, // op=0x1f1 byte=255
                { 127743, 144 }, // op=0x1f2 byte=255
                { 127999, 32 }, // op=0x1f3 byte=255
                { 128255, 16 }, // op=0x1f4 byte=255
                { 128256, 95 }, // op=0x1f5 byte=0
                { 128257, 9 }, // op=0x1f5 byte=1
                { 128767, 24 }, // op=0x1f6 byte=255
                { 129279, 96 }, // op=0x1f8 byte=255
                { 129535, 96 }, // op=0x1f9 byte=255
                { 129791, 96 }, // op=0x1fa byte=255
                { 1048831, 688 }, // op=0x1000 byte=255
                { 1048832, 200 }, // op=0x1001 byte=0
                { 1048833, 11 }, // op=0x1001 byte=1
                { 1048834, 5 }, // op=0x1001 byte=2
                { 1049087, 6608 }, // op=0x1001 byte=255
                { 1049088, 153 }, // op=0x1002 byte=0
                { 1049089, 5 }, // op=0x1002 byte=1
                { 1049090, 4 }, // op=0x1002 byte=2
                { 1049092, 2 }, // op=0x1002 byte=4
                { 1049093, 1 }, // op=0x1002 byte=5
                { 1049094, 2 }, // op=0x1002 byte=6
                { 1049095, 1 }, // op=0x1002 byte=7
                { 1049343, 5408 }, // op=0x1002 byte=255
                { 1049344, 748 }, // op=0x1003 byte=0
                { 1049345, 34 }, // op=0x1003 byte=1
                { 1049346, 5 }, // op=0x1003 byte=2
                { 1049347, 3 }, // op=0x1003 byte=3
                { 1049348, 1 }, // op=0x1003 byte=4
                { 1049382, 1 }, // op=0x1003 byte=38
                { 1049599, 8080 }, // op=0x1003 byte=255
                { 1049600, 513 }, // op=0x1004 byte=0
                { 1049601, 40 }, // op=0x1004 byte=1
                { 1049602, 2 }, // op=0x1004 byte=2
                { 1049603, 2 }, // op=0x1004 byte=3
                { 1049604, 1 }, // op=0x1004 byte=4
                { 1049605, 1 }, // op=0x1004 byte=5
                { 1049606, 1 }, // op=0x1004 byte=6
                { 1049607, 1 }, // op=0x1004 byte=7
                { 1049608, 1 }, // op=0x1004 byte=8
                { 1049609, 1 }, // op=0x1004 byte=9
                { 1049620, 1 }, // op=0x1004 byte=20
                { 1049621, 1 }, // op=0x1004 byte=21
                { 1049622, 1 }, // op=0x1004 byte=22
                { 1049625, 2 }, // op=0x1004 byte=25
                { 1049627, 4 }, // op=0x1004 byte=27
                { 1049628, 3 }, // op=0x1004 byte=28
                { 1049655, 3 }, // op=0x1004 byte=55
                { 1049656, 2 }, // op=0x1004 byte=56
                { 1049682, 2 }, // op=0x1004 byte=82
                { 1049684, 4 }, // op=0x1004 byte=84
                { 1049711, 4 }, // op=0x1004 byte=111
                { 1049712, 3 }, // op=0x1004 byte=112
                { 1049739, 3 }, // op=0x1004 byte=139
                { 1049740, 2 }, // op=0x1004 byte=140
                { 1049767, 2 }, // op=0x1004 byte=167
                { 1049855, 8952 }, // op=0x1004 byte=255
                { 1049856, 309 }, // op=0x1005 byte=0
                { 1049857, 6 }, // op=0x1005 byte=1
                { 1049858, 2 }, // op=0x1005 byte=2
                { 1049859, 11 }, // op=0x1005 byte=3
                { 1049881, 8 }, // op=0x1005 byte=25
                { 1050111, 7376 }, // op=0x1005 byte=255
                { 1050112, 660 }, // op=0x1006 byte=0
                { 1050113, 37 }, // op=0x1006 byte=1
                { 1050114, 5 }, // op=0x1006 byte=2
                { 1050115, 5 }, // op=0x1006 byte=3
                { 1050116, 1 }, // op=0x1006 byte=4
                { 1050117, 3 }, // op=0x1006 byte=5
                { 1050118, 1 }, // op=0x1006 byte=6
                { 1050119, 1 }, // op=0x1006 byte=7
                { 1050120, 2 }, // op=0x1006 byte=8
                { 1050121, 1 }, // op=0x1006 byte=9
                { 1050123, 1 }, // op=0x1006 byte=11
                { 1050141, 2 }, // op=0x1006 byte=29
                { 1050148, 1 }, // op=0x1006 byte=36
                { 1050149, 1 }, // op=0x1006 byte=37
                { 1050151, 2 }, // op=0x1006 byte=39
                { 1050152, 1 }, // op=0x1006 byte=40
                { 1050156, 1 }, // op=0x1006 byte=44
                { 1050159, 1 }, // op=0x1006 byte=47
                { 1050160, 1 }, // op=0x1006 byte=48
                { 1050163, 1 }, // op=0x1006 byte=51
                { 1050166, 1 }, // op=0x1006 byte=54
                { 1050167, 1 }, // op=0x1006 byte=55
                { 1050185, 2 }, // op=0x1006 byte=73
                { 1050190, 1 }, // op=0x1006 byte=78
                { 1050191, 1 }, // op=0x1006 byte=79
                { 1050193, 2 }, // op=0x1006 byte=81
                { 1050196, 3 }, // op=0x1006 byte=84
                { 1050197, 1 }, // op=0x1006 byte=85
                { 1050203, 1 }, // op=0x1006 byte=91
                { 1050213, 1 }, // op=0x1006 byte=101
                { 1050220, 2 }, // op=0x1006 byte=108
                { 1050221, 2 }, // op=0x1006 byte=109
                { 1050226, 2 }, // op=0x1006 byte=114
                { 1050228, 1 }, // op=0x1006 byte=116
                { 1050241, 2 }, // op=0x1006 byte=129
                { 1050247, 3 }, // op=0x1006 byte=135
                { 1050252, 2 }, // op=0x1006 byte=140
                { 1050260, 2 }, // op=0x1006 byte=148
                { 1050263, 1 }, // op=0x1006 byte=151
                { 1050270, 1 }, // op=0x1006 byte=158
                { 1050367, 5384 }, // op=0x1006 byte=255
                { 1050368, 273 }, // op=0x1007 byte=0
                { 1050369, 9 }, // op=0x1007 byte=1
                { 1050370, 10 }, // op=0x1007 byte=2
                { 1050371, 4 }, // op=0x1007 byte=3
                { 1050372, 4 }, // op=0x1007 byte=4
                { 1050374, 2 }, // op=0x1007 byte=6
                { 1050623, 13538 }, // op=0x1007 byte=255
                { 1050624, 168 }, // op=0x1008 byte=0
                { 1050625, 7 }, // op=0x1008 byte=1
                { 1050626, 1 }, // op=0x1008 byte=2
                { 1050627, 1 }, // op=0x1008 byte=3
                { 1050628, 1 }, // op=0x1008 byte=4
                { 1050879, 3830 }, // op=0x1008 byte=255
                { 1050880, 661 }, // op=0x1009 byte=0
                { 1050881, 35 }, // op=0x1009 byte=1
                { 1050882, 36 }, // op=0x1009 byte=2
                { 1050884, 6 }, // op=0x1009 byte=4
                { 1050886, 6 }, // op=0x1009 byte=6
                { 1051135, 5728 }, // op=0x1009 byte=255
                { 1051136, 316 }, // op=0x100a byte=0
                { 1051137, 18 }, // op=0x100a byte=1
                { 1051138, 3 }, // op=0x100a byte=2
                { 1051139, 1 }, // op=0x100a byte=3
                { 1051140, 1 }, // op=0x100a byte=4
                { 1051143, 1 }, // op=0x100a byte=7
                { 1051391, 3188 }, // op=0x100a byte=255
                { 1051392, 315 }, // op=0x100b byte=0
                { 1051393, 20 }, // op=0x100b byte=1
                { 1051394, 4 }, // op=0x100b byte=2
                { 1051395, 1 }, // op=0x100b byte=3
                { 1051397, 1 }, // op=0x100b byte=5
                { 1051402, 1 }, // op=0x100b byte=10
                { 1051647, 4154 }, // op=0x100b byte=255
                { 1051648, 251 }, // op=0x100c byte=0
                { 1051649, 15 }, // op=0x100c byte=1
                { 1051650, 1 }, // op=0x100c byte=2
                { 1051651, 2 }, // op=0x100c byte=3
                { 1051652, 1 }, // op=0x100c byte=4
                { 1051653, 1 }, // op=0x100c byte=5
                { 1051654, 1 }, // op=0x100c byte=6
                { 1051658, 1 }, // op=0x100c byte=10
                { 1051660, 1 }, // op=0x100c byte=12
                { 1051661, 1 }, // op=0x100c byte=13
                { 1051662, 1 }, // op=0x100c byte=14
                { 1051663, 1 }, // op=0x100c byte=15
                { 1051665, 1 }, // op=0x100c byte=17
                { 1051666, 1 }, // op=0x100c byte=18
                { 1051668, 1 }, // op=0x100c byte=20
                { 1051903, 3632 }, // op=0x100c byte=255
                { 1051904, 354 }, // op=0x100d byte=0
                { 1051905, 13 }, // op=0x100d byte=1
                { 1051906, 5 }, // op=0x100d byte=2
                { 1051907, 6 }, // op=0x100d byte=3
                { 1051908, 6 }, // op=0x100d byte=4
                { 1051909, 1 }, // op=0x100d byte=5
                { 1051910, 1 }, // op=0x100d byte=6
                { 1051911, 3 }, // op=0x100d byte=7
                { 1051912, 2 }, // op=0x100d byte=8
                { 1051913, 2 }, // op=0x100d byte=9
                { 1051914, 2 }, // op=0x100d byte=10
                { 1051915, 2 }, // op=0x100d byte=11
                { 1051924, 3 }, // op=0x100d byte=20
                { 1052159, 4152 }, // op=0x100d byte=255
                { 1052160, 383 }, // op=0x100e byte=0
                { 1052161, 24 }, // op=0x100e byte=1
                { 1052162, 6 }, // op=0x100e byte=2
                { 1052163, 1 }, // op=0x100e byte=3
                { 1052415, 3418 }, // op=0x100e byte=255
                { 1052416, 660 }, // op=0x100f byte=0
                { 1052417, 20 }, // op=0x100f byte=1
                { 1052418, 9 }, // op=0x100f byte=2
                { 1052419, 3 }, // op=0x100f byte=3
                { 1052420, 4 }, // op=0x100f byte=4
                { 1052671, 4104 }, // op=0x100f byte=255
                { 1052672, 972 }, // op=0x1010 byte=0
                { 1052673, 27 }, // op=0x1010 byte=1
                { 1052674, 9 }, // op=0x1010 byte=2
                { 1052675, 10 }, // op=0x1010 byte=3
                { 1052676, 2 }, // op=0x1010 byte=4
                { 1052677, 1 }, // op=0x1010 byte=5
                { 1052678, 1 }, // op=0x1010 byte=6
                { 1052679, 1 }, // op=0x1010 byte=7
                { 1052680, 1 }, // op=0x1010 byte=8
                { 1052927, 3784 }, // op=0x1010 byte=255
                { 1052928, 498 }, // op=0x1011 byte=0
                { 1052929, 13 }, // op=0x1011 byte=1
                { 1052930, 4 }, // op=0x1011 byte=2
                { 1052931, 2 }, // op=0x1011 byte=3
                { 1052932, 1 }, // op=0x1011 byte=4
                { 1052934, 1 }, // op=0x1011 byte=6
                { 1052961, 1 }, // op=0x1011 byte=33
                { 1053183, 3288 }, // op=0x1011 byte=255
                { 1053184, 151 }, // op=0x1012 byte=0
                { 1053185, 5 }, // op=0x1012 byte=1
                { 1053186, 2 }, // op=0x1012 byte=2
                { 1053234, 2 }, // op=0x1012 byte=50
                { 1053439, 2576 }, // op=0x1012 byte=255
                { 1053440, 83 }, // op=0x1013 byte=0
                { 1053441, 5 }, // op=0x1013 byte=1
                { 1053695, 3792 }, // op=0x1013 byte=255
                { 1053696, 811 }, // op=0x1014 byte=0
                { 1053697, 3 }, // op=0x1014 byte=1
                { 1053746, 34 }, // op=0x1014 byte=50
                { 1053951, 3096 }, // op=0x1014 byte=255
                { 1053952, 350 }, // op=0x1015 byte=0
                { 1053953, 16 }, // op=0x1015 byte=1
                { 1053962, 26 }, // op=0x1015 byte=10
                { 1054207, 3312 }, // op=0x1015 byte=255
                { 1054208, 475 }, // op=0x1016 byte=0
                { 1054209, 9 }, // op=0x1016 byte=1
                { 1054210, 4 }, // op=0x1016 byte=2
                { 1054211, 4 }, // op=0x1016 byte=3
                { 1054212, 1 }, // op=0x1016 byte=4
                { 1054213, 1 }, // op=0x1016 byte=5
                { 1054214, 1 }, // op=0x1016 byte=6
                { 1054215, 1 }, // op=0x1016 byte=7
                { 1054216, 1 }, // op=0x1016 byte=8
                { 1054228, 6 }, // op=0x1016 byte=20
                { 1054238, 6 }, // op=0x1016 byte=30
                { 1054252, 4 }, // op=0x1016 byte=44
                { 1054258, 11 }, // op=0x1016 byte=50
                { 1054408, 12 }, // op=0x1016 byte=200
                { 1054463, 3048 }, // op=0x1016 byte=255
                { 1054464, 30 }, // op=0x1017 byte=0
                { 1054465, 1 }, // op=0x1017 byte=1
                { 1054564, 1 }, // op=0x1017 byte=100
                { 1054719, 3432 }, // op=0x1017 byte=255
                { 1054720, 300 }, // op=0x1018 byte=0
                { 1054721, 23 }, // op=0x1018 byte=1
                { 1054722, 2 }, // op=0x1018 byte=2
                { 1054723, 1 }, // op=0x1018 byte=3
                { 1054724, 1 }, // op=0x1018 byte=4
                { 1054754, 1 }, // op=0x1018 byte=34
                { 1054975, 1864 }, // op=0x1018 byte=255
                { 1054976, 225 }, // op=0x1019 byte=0
                { 1054977, 2 }, // op=0x1019 byte=1
                { 1054980, 1 }, // op=0x1019 byte=4
                { 1054986, 28 }, // op=0x1019 byte=10
                { 1055231, 1800 }, // op=0x1019 byte=255
                { 1055232, 128 }, // op=0x101a byte=0
                { 1055233, 6 }, // op=0x101a byte=1
                { 1055282, 2 }, // op=0x101a byte=50
                { 1055487, 1600 }, // op=0x101a byte=255
                { 1055488, 44 }, // op=0x101b byte=0
                { 1055489, 2 }, // op=0x101b byte=1
                { 1055528, 2 }, // op=0x101b byte=40
                { 1055743, 2280 }, // op=0x101b byte=255
                { 1055999, 1944 }, // op=0x101c byte=255
                { 1056000, 121 }, // op=0x101d byte=0
                { 1056001, 7 }, // op=0x101d byte=1
                { 1056255, 1944 }, // op=0x101d byte=255
                { 1056256, 709 }, // op=0x101e byte=0
                { 1056257, 2 }, // op=0x101e byte=1
                { 1056258, 4 }, // op=0x101e byte=2
                { 1056260, 2 }, // op=0x101e byte=4
                { 1056262, 2 }, // op=0x101e byte=6
                { 1056306, 80 }, // op=0x101e byte=50
                { 1056356, 9 }, // op=0x101e byte=100
                { 1056511, 1744 }, // op=0x101e byte=255
                { 1056512, 163 }, // op=0x101f byte=0
                { 1056513, 3 }, // op=0x101f byte=1
                { 1056515, 8 }, // op=0x101f byte=3
                { 1056516, 2 }, // op=0x101f byte=4
                { 1056528, 8 }, // op=0x101f byte=16
                { 1056744, 8 }, // op=0x101f byte=232
                { 1056767, 1792 }, // op=0x101f byte=255
                { 1057023, 1368 }, // op=0x1020 byte=255
                { 1057024, 120 }, // op=0x1021 byte=0
                { 1057025, 8 }, // op=0x1021 byte=1
                { 1057279, 2232 }, // op=0x1021 byte=255
                { 1057280, 15 }, // op=0x1022 byte=0
                { 1057281, 1 }, // op=0x1022 byte=1
                { 1057535, 1128 }, // op=0x1022 byte=255
                { 1057791, 1568 }, // op=0x1023 byte=255
                { 1057792, 178 }, // op=0x1024 byte=0
                { 1057793, 5 }, // op=0x1024 byte=1
                { 1057794, 1 }, // op=0x1024 byte=2
                { 1057799, 2 }, // op=0x1024 byte=7
                { 1057801, 2 }, // op=0x1024 byte=9
                { 1057802, 1 }, // op=0x1024 byte=10
                { 1057880, 1 }, // op=0x1024 byte=88
                { 1057892, 2 }, // op=0x1024 byte=100
                { 1058047, 936 }, // op=0x1024 byte=255
                { 1058048, 51 }, // op=0x1025 byte=0
                { 1058049, 5 }, // op=0x1025 byte=1
                { 1058050, 2 }, // op=0x1025 byte=2
                { 1058068, 4 }, // op=0x1025 byte=20
                { 1058108, 2 }, // op=0x1025 byte=60
                { 1058303, 1736 }, // op=0x1025 byte=255
                { 1058304, 15 }, // op=0x1026 byte=0
                { 1058305, 1 }, // op=0x1026 byte=1
                { 1058559, 832 }, // op=0x1026 byte=255
                { 1058560, 182 }, // op=0x1027 byte=0
                { 1058570, 22 }, // op=0x1027 byte=10
                { 1058610, 4 }, // op=0x1027 byte=50
                { 1058815, 1256 }, // op=0x1027 byte=255
                { 1058816, 91 }, // op=0x1028 byte=0
                { 1058817, 2 }, // op=0x1028 byte=1
                { 1058818, 4 }, // op=0x1028 byte=2
                { 1058819, 7 }, // op=0x1028 byte=3
                { 1059071, 1816 }, // op=0x1028 byte=255
                { 1059072, 233 }, // op=0x1029 byte=0
                { 1059073, 7 }, // op=0x1029 byte=1
                { 1059074, 18 }, // op=0x1029 byte=2
                { 1059075, 2 }, // op=0x1029 byte=3
                { 1059076, 2 }, // op=0x1029 byte=4
                { 1059327, 1370 }, // op=0x1029 byte=255
                { 1059328, 105 }, // op=0x102a byte=0
                { 1059329, 7 }, // op=0x102a byte=1
                { 1059583, 1424 }, // op=0x102a byte=255
                { 1059584, 45 }, // op=0x102b byte=0
                { 1059585, 3 }, // op=0x102b byte=1
                { 1059839, 1560 }, // op=0x102b byte=255
                { 1059840, 104 }, // op=0x102c byte=0
                { 1059841, 18 }, // op=0x102c byte=1
                { 1059842, 4 }, // op=0x102c byte=2
                { 1059844, 4 }, // op=0x102c byte=4
                { 1059847, 2 }, // op=0x102c byte=7
                { 1059849, 4 }, // op=0x102c byte=9
                { 1060095, 1096 }, // op=0x102c byte=255
                { 1060096, 71 }, // op=0x102d byte=0
                { 1060097, 9 }, // op=0x102d byte=1
                { 1060351, 648 }, // op=0x102d byte=255
                { 1060352, 70 }, // op=0x102e byte=0
                { 1060353, 8 }, // op=0x102e byte=1
                { 1060354, 2 }, // op=0x102e byte=2
                { 1060607, 1296 }, // op=0x102e byte=255
                { 1060608, 38 }, // op=0x102f byte=0
                { 1060614, 1 }, // op=0x102f byte=6
                { 1060616, 1 }, // op=0x102f byte=8
                { 1060863, 888 }, // op=0x102f byte=255
                { 1060864, 30 }, // op=0x1030 byte=0
                { 1060865, 2 }, // op=0x1030 byte=1
                { 1061119, 672 }, // op=0x1030 byte=255
                { 1061120, 21 }, // op=0x1031 byte=0
                { 1061121, 3 }, // op=0x1031 byte=1
                { 1061375, 472 }, // op=0x1031 byte=255
                { 1061376, 7 }, // op=0x1032 byte=0
                { 1061381, 1 }, // op=0x1032 byte=5
                { 1061631, 1336 }, // op=0x1032 byte=255
                { 1061632, 22 }, // op=0x1033 byte=0
                { 1061633, 2 }, // op=0x1033 byte=1
                { 1061887, 1224 }, // op=0x1033 byte=255
                { 1062143, 568 }, // op=0x1034 byte=255
                { 1062144, 37 }, // op=0x1035 byte=0
                { 1062145, 2 }, // op=0x1035 byte=1
                { 1062146, 1 }, // op=0x1035 byte=2
                { 1062399, 936 }, // op=0x1035 byte=255
                { 1062655, 416 }, // op=0x1036 byte=255
                { 1062656, 14 }, // op=0x1037 byte=0
                { 1062658, 2 }, // op=0x1037 byte=2
                { 1062911, 504 }, // op=0x1037 byte=255
                { 1062912, 12 }, // op=0x1038 byte=0
                { 1062920, 1 }, // op=0x1038 byte=8
                { 1062930, 1 }, // op=0x1038 byte=18
                { 1062965, 1 }, // op=0x1038 byte=53
                { 1062966, 1 }, // op=0x1038 byte=54
                { 1063167, 936 }, // op=0x1038 byte=255
                { 1063423, 1176 }, // op=0x1039 byte=255
                { 1063679, 9088 }, // op=0x103a byte=255
                { 1063935, 936 }, // op=0x103b byte=255
                { 1063936, 1085 }, // op=0x103c byte=0
                { 1063937, 75 }, // op=0x103c byte=1
                { 1064191, 648 }, // op=0x103c byte=255
                { 1064192, 560 }, // op=0x103d byte=0
                { 1064193, 40 }, // op=0x103d byte=1
                { 1064447, 600 }, // op=0x103d byte=255
                { 1064448, 560 }, // op=0x103e byte=0
                { 1064449, 40 }, // op=0x103e byte=1
                { 1064703, 256 }, // op=0x103e byte=255
                { 1064959, 536 }, // op=0x103f byte=255
                { 1064960, 63 }, // op=0x1040 byte=0
                { 1064961, 1 }, // op=0x1040 byte=1
                { 1064962, 1 }, // op=0x1040 byte=2
                { 1064963, 1 }, // op=0x1040 byte=3
                { 1064964, 1 }, // op=0x1040 byte=4
                { 1064965, 1 }, // op=0x1040 byte=5
                { 1064966, 1 }, // op=0x1040 byte=6
                { 1064967, 1 }, // op=0x1040 byte=7
                { 1065215, 1322 }, // op=0x1040 byte=255
                { 1065471, 432 }, // op=0x1041 byte=255
                { 1065727, 352 }, // op=0x1042 byte=255
                { 1065728, 12 }, // op=0x1043 byte=0
                { 1065730, 1 }, // op=0x1043 byte=2
                { 1065733, 1 }, // op=0x1043 byte=5
                { 1065979, 1 }, // op=0x1043 byte=251
                { 1065983, 473 }, // op=0x1043 byte=255
                { 1065984, 120 }, // op=0x1044 byte=0
                { 1065985, 8 }, // op=0x1044 byte=1
                { 1066239, 1792 }, // op=0x1044 byte=255
                { 1066240, 210 }, // op=0x1045 byte=0
                { 1066241, 14 }, // op=0x1045 byte=1
                { 1066495, 1392 }, // op=0x1045 byte=255
                { 1066751, 664 }, // op=0x1046 byte=255
                { 1067007, 240 }, // op=0x1047 byte=255
                { 1067263, 104 }, // op=0x1048 byte=255
                { 1067519, 136 }, // op=0x1049 byte=255
                { 1067520, 74 }, // op=0x104a byte=0
                { 1067521, 6 }, // op=0x104a byte=1
                { 1067775, 216 }, // op=0x104a byte=255
                { 1068031, 128 }, // op=0x104b byte=255
                { 1068287, 1232 }, // op=0x104c byte=255
                { 1068288, 18 }, // op=0x104d byte=0
                { 1068543, 750 }, // op=0x104d byte=255
                { 1068799, 1816 }, // op=0x104e byte=255
                { 1068800, 78 }, // op=0x104f byte=0
                { 1068801, 26 }, // op=0x104f byte=1
                { 1069055, 3624 }, // op=0x104f byte=255
                { 1069056, 78 }, // op=0x1050 byte=0
                { 1069057, 26 }, // op=0x1050 byte=1
                { 1069311, 3584 }, // op=0x1050 byte=255
                { 1069567, 64 }, // op=0x1051 byte=255
                { 1069823, 712 }, // op=0x1052 byte=255
                { 1070079, 104 }, // op=0x1053 byte=255
                { 1070335, 152 }, // op=0x1054 byte=255
                { 1070336, 23 }, // op=0x1055 byte=0
                { 1070337, 1 }, // op=0x1055 byte=1
                { 1070591, 168 }, // op=0x1055 byte=255
                { 1070847, 248 }, // op=0x1056 byte=255
                { 1071103, 96 }, // op=0x1057 byte=255
                { 1071359, 120 }, // op=0x1058 byte=255
                { 1071360, 30 }, // op=0x1059 byte=0
                { 1071361, 2 }, // op=0x1059 byte=1
                { 1071615, 56 }, // op=0x1059 byte=255
                { 1071871, 112 }, // op=0x105a byte=255
                { 1072127, 128 }, // op=0x105b byte=255
                { 1072383, 216 }, // op=0x105c byte=255
                { 1072639, 248 }, // op=0x105d byte=255
                { 1072895, 352 }, // op=0x105e byte=255
                { 1073151, 368 }, // op=0x105f byte=255
                { 1073407, 320 }, // op=0x1060 byte=255
                { 1073663, 48 }, // op=0x1061 byte=255
                { 1073919, 552 }, // op=0x1062 byte=255
                { 1073920, 55 }, // op=0x1063 byte=0
                { 1073921, 9 }, // op=0x1063 byte=1
                { 1074175, 192 }, // op=0x1063 byte=255
                { 1074431, 56 }, // op=0x1064 byte=255
                { 1074432, 30 }, // op=0x1065 byte=0
                { 1074433, 2 }, // op=0x1065 byte=1
                { 1074687, 56 }, // op=0x1065 byte=255
                { 1074943, 800 }, // op=0x1066 byte=255
                { 1075199, 24 }, // op=0x1067 byte=255
                { 1075711, 144 }, // op=0x1069 byte=255
                { 1075967, 256 }, // op=0x106a byte=255
                { 1076223, 632 }, // op=0x106b byte=255
                { 1076479, 560 }, // op=0x106c byte=255
                { 1076735, 888 }, // op=0x106d byte=255
                { 1076736, 805 }, // op=0x106e byte=0
                { 1076746, 20 }, // op=0x106e byte=10
                { 1076751, 12 }, // op=0x106e byte=15
                { 1076756, 22 }, // op=0x106e byte=20
                { 1076761, 21 }, // op=0x106e byte=25
                { 1076766, 14 }, // op=0x106e byte=30
                { 1076786, 12 }, // op=0x106e byte=50
                { 1076806, 14 }, // op=0x106e byte=70
                { 1076991, 136 }, // op=0x106e byte=255
                { 1077247, 16 }, // op=0x106f byte=255
                { 1077503, 4208 }, // op=0x1070 byte=255
                { 1077759, 4128 }, // op=0x1071 byte=255
                { 1078015, 400 }, // op=0x1072 byte=255
                { 1078271, 1736 }, // op=0x1073 byte=255
                { 1078272, 702 }, // op=0x1074 byte=0
                { 1078273, 117 }, // op=0x1074 byte=1
                { 1078274, 15 }, // op=0x1074 byte=2
                { 1078276, 11 }, // op=0x1074 byte=4
                { 1078278, 75 }, // op=0x1074 byte=6
                { 1078281, 16 }, // op=0x1074 byte=9
                { 1078527, 184 }, // op=0x1074 byte=255
                { 1078783, 584 }, // op=0x1075 byte=255
                { 1079039, 224 }, // op=0x1076 byte=255
                { 1079040, 690 }, // op=0x1077 byte=0
                { 1079041, 115 }, // op=0x1077 byte=1
                { 1079043, 32 }, // op=0x1077 byte=3
                { 1079045, 17 }, // op=0x1077 byte=5
                { 1079046, 50 }, // op=0x1077 byte=6
                { 1079049, 16 }, // op=0x1077 byte=9
                { 1079551, 1312 }, // op=0x1078 byte=255
                { 1079807, 136 }, // op=0x1079 byte=255
                { 1080063, 128 }, // op=0x107a byte=255
                { 1080319, 296 }, // op=0x107b byte=255
                { 1080575, 256 }, // op=0x107c byte=255
                { 1080831, 128 }, // op=0x107d byte=255
                { 1081087, 128 }, // op=0x107e byte=255
                { 1081343, 128 }, // op=0x107f byte=255
                { 1081599, 24 }, // op=0x1080 byte=255
                { 1081855, 144 }, // op=0x1081 byte=255
                { 1082111, 240 }, // op=0x1082 byte=255
                { 1082623, 8 }, // op=0x1084 byte=255
                { 1082879, 8 }, // op=0x1085 byte=255
                { 1083135, 360 }, // op=0x1086 byte=255
                { 1083903, 120 }, // op=0x1089 byte=255
                { 1084159, 104 }, // op=0x108a byte=255
                { 1084415, 40 }, // op=0x108b byte=255
                { 1084927, 328 }, // op=0x108d byte=255
                { 1085183, 224 }, // op=0x108e byte=255
                { 1085951, 208 }, // op=0x1091 byte=255
                { 1086463, 112 }, // op=0x1093 byte=255
                { 1086719, 120 }, // op=0x1094 byte=255
                { 1086975, 96 }, // op=0x1095 byte=255
                { 1087231, 264 }, // op=0x1096 byte=255
                { 1087999, 192 }, // op=0x1099 byte=255
                { 1088255, 584 }, // op=0x109a byte=255
                { 1088511, 584 }, // op=0x109b byte=255
                { 1089279, 88 }, // op=0x109e byte=255
                { 1090303, 80 }, // op=0x10a2 byte=255
                { 1090559, 104 }, // op=0x10a3 byte=255
                { 1091327, 104 }, // op=0x10a6 byte=255
                { 1092095, 104 }, // op=0x10a9 byte=255
                { 1092351, 96 }, // op=0x10aa byte=255
                { 1092607, 112 }, // op=0x10ab byte=255
                { 1092863, 112 }, // op=0x10ac byte=255
                { 1093119, 320 }, // op=0x10ad byte=255
                { 1093375, 168 }, // op=0x10ae byte=255
                { 1093631, 96 }, // op=0x10af byte=255
                { 1094399, 128 }, // op=0x10b2 byte=255
                { 1094655, 128 }, // op=0x10b3 byte=255
                { 1094911, 120 }, // op=0x10b4 byte=255
                { 1095167, 80 }, // op=0x10b5 byte=255
                { 1095423, 224 }, // op=0x10b6 byte=255
                { 1095679, 168 }, // op=0x10b7 byte=255
                { 1095680, 192 }, // op=0x10b8 byte=0
                { 1095681, 32 }, // op=0x10b8 byte=1
                { 1095682, 16 }, // op=0x10b8 byte=2
                { 1095684, 16 }, // op=0x10b8 byte=4
                { 1096191, 256 }, // op=0x10b9 byte=255
                { 1096447, 240 }, // op=0x10ba byte=255
                { 1096703, 288 }, // op=0x10bb byte=255
                { 1096959, 40 }, // op=0x10bc byte=255
                { 1096960, 55 }, // op=0x10bd byte=0
                { 1096961, 10 }, // op=0x10bd byte=1
                { 1096965, 1 }, // op=0x10bd byte=5
                { 1096967, 4 }, // op=0x10bd byte=7
                { 1097201, 4 }, // op=0x10bd byte=241
                { 1097206, 1 }, // op=0x10bd byte=246
                { 1097215, 29 }, // op=0x10bd byte=255
                { 1097216, 55 }, // op=0x10be byte=0
                { 1097217, 10 }, // op=0x10be byte=1
                { 1097221, 1 }, // op=0x10be byte=5
                { 1097223, 4 }, // op=0x10be byte=7
                { 1097457, 4 }, // op=0x10be byte=241
                { 1097467, 1 }, // op=0x10be byte=251
                { 1097471, 85 }, // op=0x10be byte=255
                { 1097727, 56 }, // op=0x10bf byte=255
                { 1097983, 48 }, // op=0x10c0 byte=255
                { 1098239, 304 }, // op=0x10c1 byte=255
                { 1098495, 376 }, // op=0x10c2 byte=255
                { 1098751, 8 }, // op=0x10c3 byte=255
                { 1099007, 16 }, // op=0x10c4 byte=255
                { 1101311, 16 }, // op=0x10cd byte=255
                { 1101567, 16 }, // op=0x10ce byte=255
                { 1101823, 16 }, // op=0x10cf byte=255
                { 1102079, 16 }, // op=0x10d0 byte=255
                { 1102335, 32 }, // op=0x10d1 byte=255
                { 1102591, 16 }, // op=0x10d2 byte=255
                { 1102847, 128 }, // op=0x10d3 byte=255
                { 1103103, 16 }, // op=0x10d4 byte=255
                { 1103871, 16 }, // op=0x10d7 byte=255
                { 1104639, 40 }, // op=0x10da byte=255
                { 1104895, 32 }, // op=0x10db byte=255
                { 1106175, 16 }, // op=0x10e0 byte=255
                { 1107455, 8 }, // op=0x10e5 byte=255
                { 1107711, 16 }, // op=0x10e6 byte=255
                { 1109503, 24 }, // op=0x10ed byte=255
                { 1109759, 32 }, // op=0x10ee byte=255
                { 1110015, 16 }, // op=0x10ef byte=255
                { 1111295, 16 }, // op=0x10f4 byte=255
                { 1112575, 464 }, // op=0x10f9 byte=255
                { 1112831, 16 }, // op=0x10fa byte=255
                { 1113087, 16 }, // op=0x10fb byte=255
                { 1113343, 16 }, // op=0x10fc byte=255
                { 1113599, 96 }, // op=0x10fd byte=255
                { 1113855, 16 }, // op=0x10fe byte=255
                { 1114111, 64 }, // op=0x10ff byte=255
                { 1114367, 16 }, // op=0x1100 byte=255
                { 1115903, 24 }, // op=0x1106 byte=255
                { 1117183, 16 }, // op=0x110b byte=255
                { 1117439, 24 }, // op=0x110c byte=255
                { 1117695, 8 }, // op=0x110d byte=255
                { 1117951, 8 }, // op=0x110e byte=255
                { 1118207, 8 }, // op=0x110f byte=255
                { 1118463, 8 }, // op=0x1110 byte=255
                { 1119231, 32 }, // op=0x1113 byte=255
                { 1120511, 16 }, // op=0x1118 byte=255
                { 1120767, 64 }, // op=0x1119 byte=255
                { 1121535, 16 }, // op=0x111c byte=255
                { 1122047, 16 }, // op=0x111e byte=255
                { 1122303, 16 }, // op=0x111f byte=255
                { 1123071, 64 }, // op=0x1122 byte=255
                { 1123839, 16 }, // op=0x1125 byte=255
                { 1124607, 8 }, // op=0x1128 byte=255
                { 1125375, 8 }, // op=0x112b byte=255
                { 1127679, 16 }, // op=0x1134 byte=255
                { 2097152, 7 }, // op=0x2000 byte=0
                { 2097153, 1 }, // op=0x2000 byte=1
                { 2097407, 960 }, // op=0x2000 byte=255
                { 2097663, 960 }, // op=0x2001 byte=255
                { 2097919, 1416 }, // op=0x2002 byte=255
                { 2098175, 360 }, // op=0x2003 byte=255
                { 2098431, 264 }, // op=0x2004 byte=255
                { 2098432, 7 }, // op=0x2005 byte=0
                { 2098433, 1 }, // op=0x2005 byte=1
                { 2098687, 624 }, // op=0x2005 byte=255
                { 2098688, 48 }, // op=0x2006 byte=0
                { 2098715, 1 }, // op=0x2006 byte=27
                { 2098716, 1 }, // op=0x2006 byte=28
                { 2098717, 1 }, // op=0x2006 byte=29
                { 2098718, 1 }, // op=0x2006 byte=30
                { 2098774, 2 }, // op=0x2006 byte=86
                { 2098775, 2 }, // op=0x2006 byte=87
                { 2098776, 2 }, // op=0x2006 byte=88
                { 2098777, 2 }, // op=0x2006 byte=89
                { 2098780, 2 }, // op=0x2006 byte=92
                { 2098781, 2 }, // op=0x2006 byte=93
                { 2098943, 568 }, // op=0x2006 byte=255
                { 2099199, 296 }, // op=0x2007 byte=255
                { 2099200, 14 }, // op=0x2008 byte=0
                { 2099202, 1 }, // op=0x2008 byte=2
                { 2099203, 1 }, // op=0x2008 byte=3
                { 2099455, 872 }, // op=0x2008 byte=255
                { 2099456, 7 }, // op=0x2009 byte=0
                { 2099458, 1 }, // op=0x2009 byte=2
                { 2099711, 928 }, // op=0x2009 byte=255
                { 2099712, 90 }, // op=0x200a byte=0
                { 2099713, 15 }, // op=0x200a byte=1
                { 2099715, 1 }, // op=0x200a byte=3
                { 2099717, 1 }, // op=0x200a byte=5
                { 2099718, 12 }, // op=0x200a byte=6
                { 2099721, 1 }, // op=0x200a byte=9
                { 2099967, 304 }, // op=0x200a byte=255
                { 2099968, 30 }, // op=0x200b byte=0
                { 2099971, 2 }, // op=0x200b byte=3
                { 2099972, 4 }, // op=0x200b byte=4
                { 2099974, 3 }, // op=0x200b byte=6
                { 2099983, 1 }, // op=0x200b byte=15
                { 2100223, 336 }, // op=0x200b byte=255
                { 2100224, 15 }, // op=0x200c byte=0
                { 2100225, 1 }, // op=0x200c byte=1
                { 2100479, 376 }, // op=0x200c byte=255
                { 2100735, 440 }, // op=0x200d byte=255
                { 2100736, 7 }, // op=0x200e byte=0
                { 2100792, 1 }, // op=0x200e byte=56
                { 2100991, 240 }, // op=0x200e byte=255
                { 2100992, 224 }, // op=0x200f byte=0
                { 2100993, 28 }, // op=0x200f byte=1
                { 2100994, 2 }, // op=0x200f byte=2
                { 2101015, 2 }, // op=0x200f byte=23
                { 2101247, 736 }, // op=0x200f byte=255
                { 2101248, 23 }, // op=0x2010 byte=0
                { 2101251, 1 }, // op=0x2010 byte=3
                { 2101503, 512 }, // op=0x2010 byte=255
                { 2101759, 320 }, // op=0x2011 byte=255
                { 2101760, 32 }, // op=0x2012 byte=0
                { 2101761, 4 }, // op=0x2012 byte=1
                { 2101762, 2 }, // op=0x2012 byte=2
                { 2101765, 4 }, // op=0x2012 byte=5
                { 2101770, 4 }, // op=0x2012 byte=10
                { 2101772, 2 }, // op=0x2012 byte=12
                { 2101966, 2 }, // op=0x2012 byte=206
                { 2102001, 2 }, // op=0x2012 byte=241
                { 2102006, 2 }, // op=0x2012 byte=246
                { 2102011, 2 }, // op=0x2012 byte=251
                { 2102013, 2 }, // op=0x2012 byte=253
                { 2102015, 2310 }, // op=0x2012 byte=255
                { 2102016, 34 }, // op=0x2013 byte=0
                { 2102017, 2 }, // op=0x2013 byte=1
                { 2102018, 2 }, // op=0x2013 byte=2
                { 2102019, 2 }, // op=0x2013 byte=3
                { 2102026, 6 }, // op=0x2013 byte=10
                { 2102028, 2 }, // op=0x2013 byte=12
                { 2102267, 2 }, // op=0x2013 byte=251
                { 2102271, 1790 }, // op=0x2013 byte=255
                { 2102272, 6 }, // op=0x2014 byte=0
                { 2102273, 1 }, // op=0x2014 byte=1
                { 2102279, 1 }, // op=0x2014 byte=7
                { 2102527, 48 }, // op=0x2014 byte=255
                { 2102528, 6 }, // op=0x2015 byte=0
                { 2102529, 1 }, // op=0x2015 byte=1
                { 2102533, 1 }, // op=0x2015 byte=5
                { 2102783, 136 }, // op=0x2015 byte=255
                { 2102784, 30 }, // op=0x2016 byte=0
                { 2102834, 2 }, // op=0x2016 byte=50
                { 2103039, 48 }, // op=0x2016 byte=255
                { 2103295, 80 }, // op=0x2017 byte=255
                { 2103551, 176 }, // op=0x2018 byte=255
                { 2103807, 3456 }, // op=0x2019 byte=255
                { 2104063, 432 }, // op=0x201a byte=255
                { 2104319, 200 }, // op=0x201b byte=255
                { 2104575, 112 }, // op=0x201c byte=255
                { 2104831, 920 }, // op=0x201d byte=255
                { 2105087, 3488 }, // op=0x201e byte=255
                { 2105343, 352 }, // op=0x201f byte=255
                { 2105599, 1744 }, // op=0x2020 byte=255
                { 2105855, 1616 }, // op=0x2021 byte=255
                { 2106111, 136 }, // op=0x2022 byte=255
                { 2106367, 136 }, // op=0x2023 byte=255
                { 2106623, 88 }, // op=0x2024 byte=255
                { 2106624, 352 }, // op=0x2025 byte=0
                { 2106625, 35 }, // op=0x2025 byte=1
                { 2106626, 1 }, // op=0x2025 byte=2
                { 2106627, 2 }, // op=0x2025 byte=3
                { 2106628, 2 }, // op=0x2025 byte=4
                { 2106879, 48 }, // op=0x2025 byte=255
                { 2107135, 1392 }, // op=0x2026 byte=255
                { 2107391, 240 }, // op=0x2027 byte=255
                { 2107647, 96 }, // op=0x2028 byte=255
                { 2107648, 27 }, // op=0x2029 byte=0
                { 2107649, 3 }, // op=0x2029 byte=1
                { 2107650, 1 }, // op=0x2029 byte=2
                { 2107652, 1 }, // op=0x2029 byte=4
                { 2107903, 104 }, // op=0x2029 byte=255
                { 2108159, 48 }, // op=0x202a byte=255
                { 2108160, 72 }, // op=0x202b byte=0
                { 2108161, 12 }, // op=0x202b byte=1
                { 2108166, 11 }, // op=0x202b byte=6
                { 2108169, 1 }, // op=0x202b byte=9
                { 2108415, 152 }, // op=0x202b byte=255
                { 2108671, 64 }, // op=0x202c byte=255
                { 2108927, 96 }, // op=0x202d byte=255
                { 2109183, 72 }, // op=0x202e byte=255
                { 2109439, 96 }, // op=0x202f byte=255
                { 2109695, 24 }, // op=0x2030 byte=255
                { 2109951, 32 }, // op=0x2031 byte=255
                { 2110207, 104 }, // op=0x2032 byte=255
                { 2110463, 272 }, // op=0x2033 byte=255
                { 2110719, 1296 }, // op=0x2034 byte=255
                { 2110975, 128 }, // op=0x2035 byte=255
                { 2111231, 296 }, // op=0x2036 byte=255
                { 2111487, 88 }, // op=0x2037 byte=255
                { 2111743, 128 }, // op=0x2038 byte=255
                { 2111999, 160 }, // op=0x2039 byte=255
                { 2112000, 14 }, // op=0x203a byte=0
                { 2112255, 42 }, // op=0x203a byte=255
                { 2112256, 6 }, // op=0x203b byte=0
                { 2112511, 114 }, // op=0x203b byte=255
                { 2112767, 40 }, // op=0x203c byte=255
                { 2113023, 256 }, // op=0x203d byte=255
                { 2113279, 56 }, // op=0x203e byte=255
                { 2113535, 120 }, // op=0x203f byte=255
                { 2113536, 133 }, // op=0x2040 byte=0
                { 2113546, 2 }, // op=0x2040 byte=10
                { 2113556, 3 }, // op=0x2040 byte=20
                { 2113561, 4 }, // op=0x2040 byte=25
                { 2113586, 2 }, // op=0x2040 byte=50
                { 2113606, 8 }, // op=0x2040 byte=70
                { 2113791, 56 }, // op=0x2040 byte=255
                { 2114047, 168 }, // op=0x2041 byte=255
                { 2114303, 152 }, // op=0x2042 byte=255
                { 2114559, 48 }, // op=0x2043 byte=255
                { 2114815, 112 }, // op=0x2044 byte=255
                { 2115071, 144 }, // op=0x2045 byte=255
                { 2115327, 24 }, // op=0x2046 byte=255
                { 2115583, 72 }, // op=0x2047 byte=255
                { 2115839, 16 }, // op=0x2048 byte=255
                { 2115840, 7 }, // op=0x2049 byte=0
                { 2115850, 1 }, // op=0x2049 byte=10
                { 2116095, 64 }, // op=0x2049 byte=255
                { 2116351, 400 }, // op=0x204a byte=255
                { 2116607, 120 }, // op=0x204b byte=255
                { 2116863, 256 }, // op=0x204c byte=255
                { 2117119, 648 }, // op=0x204d byte=255
                { 2117375, 16 }, // op=0x204e byte=255
                { 2117631, 40 }, // op=0x204f byte=255
                { 2117887, 256 }, // op=0x2050 byte=255
                { 2118143, 480 }, // op=0x2051 byte=255
                { 2118399, 336 }, // op=0x2052 byte=255
                { 2118655, 120 }, // op=0x2053 byte=255
                { 2118656, 15 }, // op=0x2054 byte=0
                { 2118657, 1 }, // op=0x2054 byte=1
                { 2118911, 40 }, // op=0x2054 byte=255
                { 2119167, 264 }, // op=0x2055 byte=255
                { 2119423, 64 }, // op=0x2056 byte=255
                { 2119424, 15 }, // op=0x2057 byte=0
                { 2119425, 1 }, // op=0x2057 byte=1
                { 2119679, 24 }, // op=0x2057 byte=255
                { 2119680, 30 }, // op=0x2058 byte=0
                { 2119681, 2 }, // op=0x2058 byte=1
                { 2119935, 16 }, // op=0x2058 byte=255
                { 2119936, 30 }, // op=0x2059 byte=0
                { 2119937, 2 }, // op=0x2059 byte=1
                { 2120191, 24 }, // op=0x2059 byte=255
                { 2120192, 15 }, // op=0x205a byte=0
                { 2120193, 1 }, // op=0x205a byte=1
                { 2120447, 400 }, // op=0x205a byte=255
                { 2120448, 15 }, // op=0x205b byte=0
                { 2120449, 1 }, // op=0x205b byte=1
                { 2120703, 112 }, // op=0x205b byte=255
                { 2120704, 15 }, // op=0x205c byte=0
                { 2120705, 1 }, // op=0x205c byte=1
                { 2120959, 48 }, // op=0x205c byte=255
                { 2120960, 15 }, // op=0x205d byte=0
                { 2120961, 1 }, // op=0x205d byte=1
                { 2121215, 48 }, // op=0x205d byte=255
                { 2121216, 44 }, // op=0x205e byte=0
                { 2121217, 4 }, // op=0x205e byte=1
                { 2121471, 80 }, // op=0x205e byte=255
                { 2121727, 112 }, // op=0x205f byte=255
                { 2121728, 100 }, // op=0x2060 byte=0
                { 2121729, 20 }, // op=0x2060 byte=1
                { 2121730, 4 }, // op=0x2060 byte=2
                { 2121731, 4 }, // op=0x2060 byte=3
                { 2121983, 152 }, // op=0x2060 byte=255
                { 2122239, 512 }, // op=0x2061 byte=255
                { 2122240, 8 }, // op=0x2062 byte=0
                { 2122495, 416 }, // op=0x2062 byte=255
                { 2122751, 856 }, // op=0x2063 byte=255
                { 2123007, 2088 }, // op=0x2064 byte=255
                { 2123263, 8 }, // op=0x2065 byte=255
                { 2124031, 8 }, // op=0x2068 byte=255
                { 2124287, 8 }, // op=0x2069 byte=255
                { 2124543, 1560 }, // op=0x206a byte=255
                { 2124799, 88 }, // op=0x206b byte=255
                { 2125055, 112 }, // op=0x206c byte=255
                { 2125056, 256 }, // op=0x206d byte=0
                { 2125311, 272 }, // op=0x206d byte=255
                { 2125823, 80 }, // op=0x206f byte=255
                { 2126079, 16 }, // op=0x2070 byte=255
                { 2126335, 32 }, // op=0x2071 byte=255
                { 2126592, 98 }, // op=0x2073 byte=0
                { 2126593, 14 }, // op=0x2073 byte=1
                { 2126847, 88 }, // op=0x2073 byte=255
                { 2127359, 72 }, // op=0x2075 byte=255
                { 2127615, 184 }, // op=0x2076 byte=255
                { 2127871, 16 }, // op=0x2077 byte=255
                { 2128127, 16 }, // op=0x2078 byte=255
                { 2128639, 24 }, // op=0x207a byte=255
                { 2128895, 120 }, // op=0x207b byte=255
                { 2129151, 16 }, // op=0x207c byte=255
                { 2129152, 78 }, // op=0x207d byte=0
                { 2129153, 2 }, // op=0x207d byte=1
                { 2129407, 40 }, // op=0x207d byte=255
                { 2129663, 40 }, // op=0x207e byte=255
                { 2129919, 32 }, // op=0x207f byte=255
                { 2130175, 32 }, // op=0x2080 byte=255
                { 2130431, 16 }, // op=0x2081 byte=255
                { 2130687, 16 }, // op=0x2082 byte=255
                { 2130943, 16 }, // op=0x2083 byte=255
                { 2131199, 48 }, // op=0x2084 byte=255
                { 2131455, 56 }, // op=0x2085 byte=255
                { 2131711, 32 }, // op=0x2086 byte=255
                { 2131967, 72 }, // op=0x2087 byte=255
                { 2132223, 48 }, // op=0x2088 byte=255
                { 2132479, 8 }, // op=0x2089 byte=255
                { 2132735, 920 }, // op=0x208a byte=255
                { 2132991, 56 }, // op=0x208b byte=255
                { 2133247, 56 }, // op=0x208c byte=255
                { 2133503, 56 }, // op=0x208d byte=255
                { 2133759, 64 }, // op=0x208e byte=255
                { 2134015, 72 }, // op=0x208f byte=255
                { 2134271, 72 }, // op=0x2090 byte=255
                { 2134527, 64 }, // op=0x2091 byte=255
                { 2134783, 64 }, // op=0x2092 byte=255
                { 2135039, 64 }, // op=0x2093 byte=255
                { 2135295, 24 }, // op=0x2094 byte=255
                { 2135551, 16 }, // op=0x2095 byte=255
                { 2135807, 16 }, // op=0x2096 byte=255
                { 2136063, 16 }, // op=0x2097 byte=255
                { 2136575, 80 }, // op=0x2099 byte=255
                { 2137343, 8 }, // op=0x209c byte=255
                { 2137599, 8 }, // op=0x209d byte=255
                { 2138367, 48 }, // op=0x20a0 byte=255
                { 2138368, 317 }, // op=0x20a1 byte=0
                { 2138373, 3 }, // op=0x20a1 byte=5
                { 2138378, 4 }, // op=0x20a1 byte=10
                { 2138383, 3 }, // op=0x20a1 byte=15
                { 2138388, 9 }, // op=0x20a1 byte=20
                { 2138393, 3 }, // op=0x20a1 byte=25
                { 2138398, 6 }, // op=0x20a1 byte=30
                { 2138408, 5 }, // op=0x20a1 byte=40
                { 2138409, 2 }, // op=0x20a1 byte=41
                { 2138418, 5 }, // op=0x20a1 byte=50
                { 2138423, 1 }, // op=0x20a1 byte=55
                { 2138428, 2 }, // op=0x20a1 byte=60
                { 2138438, 1 }, // op=0x20a1 byte=70
                { 2138443, 1 }, // op=0x20a1 byte=75
                { 2138524, 47 }, // op=0x20a1 byte=156
                { 2138544, 1 }, // op=0x20a1 byte=176
                { 2138549, 1 }, // op=0x20a1 byte=181
                { 2138574, 4 }, // op=0x20a1 byte=206
                { 2138584, 2 }, // op=0x20a1 byte=216
                { 2138599, 3 }, // op=0x20a1 byte=231
                { 2138614, 7 }, // op=0x20a1 byte=246
                { 2138619, 2 }, // op=0x20a1 byte=251
                { 2138623, 67 }, // op=0x20a1 byte=255
                { 2138624, 185 }, // op=0x20a2 byte=0
                { 2138634, 3 }, // op=0x20a2 byte=10
                { 2138654, 3 }, // op=0x20a2 byte=30
                { 2138664, 8 }, // op=0x20a2 byte=40
                { 2138674, 5 }, // op=0x20a2 byte=50
                { 2138684, 2 }, // op=0x20a2 byte=60
                { 2138694, 1 }, // op=0x20a2 byte=70
                { 2138704, 1 }, // op=0x20a2 byte=80
                { 2138724, 26 }, // op=0x20a2 byte=100
                { 2138830, 3 }, // op=0x20a2 byte=206
                { 2138840, 2 }, // op=0x20a2 byte=216
                { 2138860, 5 }, // op=0x20a2 byte=236
                { 2138870, 1 }, // op=0x20a2 byte=246
                { 2138879, 11 }, // op=0x20a2 byte=255
                { 2139135, 16 }, // op=0x20a3 byte=255
                { 2139391, 16 }, // op=0x20a4 byte=255
                { 2139647, 8 }, // op=0x20a5 byte=255
                { 2139903, 112 }, // op=0x20a6 byte=255
                { 2140159, 16 }, // op=0x20a7 byte=255
                { 2140671, 8 }, // op=0x20a9 byte=255
                { 2140927, 8 }, // op=0x20aa byte=255
                { 2141951, 8 }, // op=0x20ae byte=255
                { 2143231, 8 }, // op=0x20b3 byte=255
                { 2143743, 8 }, // op=0x20b5 byte=255
                { 2154239, 32 }, // op=0x20de byte=255
                { 2155007, 8 }, // op=0x20e1 byte=255
                { 2156543, 72 }, // op=0x20e7 byte=255
                { 2161919, 56 }, // op=0x20fc byte=255
                { 2162175, 168 }, // op=0x20fd byte=255
                { 2162431, 16 }, // op=0x20fe byte=255
                { 2162687, 16 }, // op=0x20ff byte=255
                { 2166015, 32 }, // op=0x210c byte=255
                { 2166271, 24 }, // op=0x210d byte=255
                { 2166527, 16 }, // op=0x210e byte=255
                { 2166783, 48 }, // op=0x210f byte=255
                { 2167039, 16 }, // op=0x2110 byte=255
                { 2167295, 40 }, // op=0x2111 byte=255
                { 2167551, 16 }, // op=0x2112 byte=255
                { 2167807, 32 }, // op=0x2113 byte=255
                { 2168063, 24 }, // op=0x2114 byte=255
                { 2168319, 16 }, // op=0x2115 byte=255
                { 2168575, 32 }, // op=0x2116 byte=255
                { 2168831, 24 }, // op=0x2117 byte=255
                { 2169087, 32 }, // op=0x2118 byte=255
                { 2169343, 96 }, // op=0x2119 byte=255
                { 2171647, 16 }, // op=0x2122 byte=255
                { 2171903, 16 }, // op=0x2123 byte=255
                { 2172159, 24 }, // op=0x2124 byte=255
                { 2172671, 40 }, // op=0x2126 byte=255
                { 2172927, 64 }, // op=0x2127 byte=255
                { 2180863, 16 }, // op=0x2146 byte=255
                { 2181119, 208 }, // op=0x2147 byte=255
                { 2181631, 16 }, // op=0x2149 byte=255
                { 2184959, 8 }, // op=0x2156 byte=255
                { 2188799, 8 }, // op=0x2165 byte=255
                { 2192127, 8 }, // op=0x2172 byte=255
                { 2192383, 8 }, // op=0x2173 byte=255
                { 2192639, 16 }, // op=0x2174 byte=255
                { 2199551, 120 }, // op=0x218f byte=255
                { 2199807, 56 }, // op=0x2190 byte=255
                { 2204927, 8 }, // op=0x21a4 byte=255
                { 2205183, 64 }, // op=0x21a5 byte=255
                { 2205439, 8 }, // op=0x21a6 byte=255
                { 2206207, 8 }, // op=0x21a9 byte=255
                { 2206463, 112 }, // op=0x21aa byte=255
                { 2210303, 32 }, // op=0x21b9 byte=255
                { 2211583, 16 }, // op=0x21be byte=255
                { 2211839, 16 }, // op=0x21bf byte=255
                { 2212095, 8 }, // op=0x21c0 byte=255
                { 2212351, 8 }, // op=0x21c1 byte=255
                { 2212607, 24 }, // op=0x21c2 byte=255
                { 2213375, 56 }, // op=0x21c5 byte=255
                { 2215679, 8 }, // op=0x21ce byte=255
                { 2216447, 8 }, // op=0x21d1 byte=255
                { 2216703, 8 }, // op=0x21d2 byte=255
                { 2216959, 8 }, // op=0x21d3 byte=255
                { 2217215, 32 }, // op=0x21d4 byte=255
                { 2217471, 8 }, // op=0x21d5 byte=255
                { 2218239, 8 }, // op=0x21d8 byte=255
                { 8388607, 1088 }, // op=0x7fff byte=255
                { 16777215, 560 }, // op=0xffff byte=255
            };
        public const int SlotChunkCensus = 1472;
        public const int SlotEntryCensus = 6969;
        public const int SprChunkCensus = 758;
        public const int SprFrameCensus = 3120;
        public const int Spr2ChunkCensus = 17140;
        public const int Spr2FrameCensus = 138948;

        // Round 61 IFF-LITERAL BHAV opcode census (tools/iff_bhav_ops.py over game-data/The Sims): the
        // exact opcode histogram of the IFF BHAV instruction stream - 713 distinct opcodes, 388503
        // instructions, per member basename LAST-wins, chunks distinct by (type,id). IFF data - the
        // opcode surface the behavior VM executes against.
        public const int BhavOpcodeDistinctCensus = 713;
public static readonly int[] BhavOpcodePairCensus = new int[] {
0, 5, 1, 673, 2, 227088, 3, 32, 4, 244, 5, 13, 6, 6259, 7, 2827, 8, 4778, 9, 26, 10, 13, 11,
350, 12, 159, 13, 2374, 14, 324, 15, 1446, 16, 702, 17, 4, 18, 2327, 19, 13, 20, 350, 21, 27,
22, 883, 23, 10411, 24, 346, 25, 690, 26, 2919, 27, 2045, 28, 2494, 29, 1711, 30, 31, 31,
7747, 32, 6666, 33, 2, 34, 58, 35, 383, 36, 2855, 37, 41, 41, 2073, 42, 2643, 43, 279, 44,
23733, 45, 2463, 46, 944, 47, 142, 48, 1185, 49, 828, 50, 1293, 51, 3921, 256, 85, 259, 33,
260, 587, 261, 12, 265, 244, 271, 1009, 272, 700, 273, 1129, 275, 183, 277, 383, 278, 124,
279, 234, 280, 6091, 281, 2633, 282, 88, 283, 37, 284, 11, 285, 103, 286, 105, 287, 161, 288,
272, 289, 1, 290, 51, 291, 13, 292, 27, 293, 16, 294, 10, 295, 275, 296, 2, 297, 30, 299, 6,
300, 16, 301, 106, 302, 106, 303, 86, 304, 5, 306, 57, 307, 51, 308, 78, 309, 65, 310, 27,
311, 2, 312, 5, 313, 2, 314, 3, 316, 3, 317, 1, 318, 1, 319, 87, 320, 193, 321, 617, 322, 8,
323, 17, 324, 64, 325, 15, 326, 1, 327, 68, 328, 1, 329, 2, 330, 2, 331, 1, 332, 118, 333,
150, 334, 85, 335, 51, 336, 13, 338, 1, 339, 29, 340, 80, 342, 18, 343, 60, 345, 3, 346, 16,
347, 103, 348, 50, 349, 42, 350, 45, 351, 37, 354, 11, 355, 253, 356, 32, 357, 84, 358, 16,
359, 15, 360, 12, 361, 28, 362, 48, 363, 439, 364, 45, 365, 171, 366, 198, 367, 35, 368, 72,
369, 15, 370, 370, 371, 19, 372, 19, 373, 16, 374, 34, 375, 22, 376, 31, 377, 18, 378, 5, 379,
44, 380, 8, 381, 14, 382, 116, 383, 24, 384, 24, 385, 22, 386, 46, 387, 8, 388, 15, 389, 35,
390, 6, 391, 14, 392, 7, 393, 20, 394, 33, 395, 5, 396, 3, 397, 448, 398, 49, 399, 69, 400,
63, 401, 179, 402, 94, 403, 193, 404, 2, 405, 102, 406, 110, 407, 65, 408, 55, 409, 17, 410,
54, 411, 9, 412, 17, 413, 12, 414, 2, 415, 45, 416, 39, 417, 46, 419, 33, 420, 1, 421, 46,
422, 8, 423, 26, 424, 99, 425, 34, 426, 5, 427, 6, 428, 23, 429, 14, 430, 7, 431, 5, 432, 20,
433, 19, 434, 9, 435, 11, 436, 1, 437, 2, 438, 54, 439, 17, 440, 1, 441, 1, 442, 1, 443, 5,
444, 7, 445, 9, 446, 89, 447, 7, 448, 7, 449, 4, 451, 9, 452, 43, 453, 5, 454, 5, 455, 1, 456,
7, 457, 5, 458, 2, 459, 2, 460, 13, 461, 11, 462, 2, 463, 21, 464, 14, 465, 2, 467, 156, 468,
116, 469, 67, 470, 7, 472, 7, 473, 1, 474, 1, 475, 1, 476, 5, 477, 9, 479, 85, 480, 5, 481, 5,
482, 4, 483, 74, 484, 7, 485, 20, 486, 14, 487, 1, 488, 13, 489, 10, 490, 15, 491, 2, 492, 64,
493, 8, 494, 5, 495, 14, 496, 13, 497, 23, 498, 18, 499, 4, 500, 2, 501, 13, 502, 3, 504, 12,
505, 12, 506, 12, 4096, 86, 4097, 853, 4098, 697, 4099, 1109, 4100, 1194, 4101, 964, 4102,
768, 4103, 1730, 4104, 501, 4105, 809, 4106, 441, 4107, 562, 4108, 489, 4109, 569, 4110, 479,
4111, 600, 4112, 601, 4113, 476, 4114, 342, 4115, 485, 4116, 493, 4117, 463, 4118, 448, 4119,
433, 4120, 274, 4121, 257, 4122, 217, 4123, 291, 4124, 243, 4125, 259, 4126, 319, 4127, 248,
4128, 171, 4129, 295, 4130, 143, 4131, 196, 4132, 141, 4133, 225, 4134, 106, 4135, 183, 4136,
240, 4137, 204, 4138, 192, 4139, 201, 4140, 154, 4141, 91, 4142, 172, 4143, 116, 4144, 88,
4145, 62, 4146, 168, 4147, 156, 4148, 71, 4149, 122, 4150, 52, 4151, 65, 4152, 119, 4153, 147,
4154, 1136, 4155, 117, 4156, 226, 4157, 150, 4158, 107, 4159, 67, 4160, 174, 4161, 54, 4162,
44, 4163, 61, 4164, 240, 4165, 202, 4166, 83, 4167, 30, 4168, 13, 4169, 17, 4170, 37, 4171,
16, 4172, 154, 4173, 96, 4174, 227, 4175, 466, 4176, 461, 4177, 8, 4178, 89, 4179, 13, 4180,
19, 4181, 24, 4182, 31, 4183, 12, 4184, 15, 4185, 11, 4186, 14, 4187, 16, 4188, 27, 4189, 31,
4190, 44, 4191, 46, 4192, 40, 4193, 6, 4194, 69, 4195, 32, 4196, 7, 4197, 11, 4198, 100, 4199,
3, 4201, 18, 4202, 32, 4203, 79, 4204, 70, 4205, 111, 4206, 132, 4207, 2, 4208, 526, 4209,
516, 4210, 50, 4211, 217, 4212, 140, 4213, 73, 4214, 28, 4215, 115, 4216, 164, 4217, 17, 4218,
16, 4219, 37, 4220, 32, 4221, 16, 4222, 16, 4223, 16, 4224, 3, 4225, 18, 4226, 30, 4228, 1,
4229, 1, 4230, 45, 4233, 15, 4234, 13, 4235, 5, 4237, 41, 4238, 28, 4241, 26, 4243, 14, 4244,
15, 4245, 12, 4246, 33, 4249, 24, 4250, 73, 4251, 73, 4254, 11, 4258, 10, 4259, 13, 4262, 13,
4265, 13, 4266, 12, 4267, 14, 4268, 14, 4269, 40, 4270, 21, 4271, 12, 4274, 16, 4275, 16,
4276, 15, 4277, 10, 4278, 28, 4279, 21, 4280, 32, 4281, 32, 4282, 30, 4283, 36, 4284, 5, 4285,
13, 4286, 20, 4287, 7, 4288, 6, 4289, 38, 4290, 47, 4291, 1, 4292, 2, 4301, 2, 4302, 2, 4303,
2, 4304, 2, 4305, 4, 4306, 2, 4307, 16, 4308, 2, 4311, 2, 4314, 5, 4315, 4, 4320, 2, 4325, 1,
4326, 2, 4333, 3, 4334, 4, 4335, 2, 4340, 2, 4345, 58, 4346, 2, 4347, 2, 4348, 2, 4349, 12,
4350, 2, 4351, 8, 4352, 2, 4358, 3, 4363, 2, 4364, 3, 4365, 1, 4366, 1, 4367, 1, 4368, 1,
4371, 4, 4376, 2, 4377, 8, 4380, 2, 4382, 2, 4383, 2, 4386, 8, 4389, 2, 4392, 1, 4395, 1,
4404, 2, 8192, 121, 8193, 120, 8194, 177, 8195, 45, 8196, 33, 8197, 79, 8198, 79, 8199, 37,
8200, 111, 8201, 117, 8202, 53, 8203, 47, 8204, 49, 8205, 55, 8206, 31, 8207, 124, 8208, 67,
8209, 40, 8210, 296, 8211, 230, 8212, 7, 8213, 18, 8214, 10, 8215, 10, 8216, 22, 8217, 432,
8218, 54, 8219, 25, 8220, 14, 8221, 115, 8222, 436, 8223, 44, 8224, 218, 8225, 202, 8226, 17,
8227, 17, 8228, 11, 8229, 55, 8230, 174, 8231, 30, 8232, 12, 8233, 17, 8234, 6, 8235, 31,
8236, 8, 8237, 12, 8238, 9, 8239, 12, 8240, 3, 8241, 4, 8242, 13, 8243, 34, 8244, 162, 8245,
16, 8246, 37, 8247, 11, 8248, 16, 8249, 20, 8250, 7, 8251, 15, 8252, 5, 8253, 32, 8254, 7,
8255, 15, 8256, 26, 8257, 21, 8258, 19, 8259, 6, 8260, 14, 8261, 18, 8262, 3, 8263, 9, 8264,
2, 8265, 9, 8266, 50, 8267, 15, 8268, 32, 8269, 81, 8270, 2, 8271, 5, 8272, 32, 8273, 60,
8274, 42, 8275, 15, 8276, 7, 8277, 33, 8278, 8, 8279, 5, 8280, 6, 8281, 7, 8282, 52, 8283, 16,
8284, 8, 8285, 8, 8286, 16, 8287, 14, 8288, 35, 8289, 64, 8290, 53, 8291, 107, 8292, 261,
8293, 1, 8296, 1, 8297, 1, 8298, 195, 8299, 11, 8300, 14, 8301, 66, 8303, 10, 8304, 2, 8305,
4, 8307, 25, 8309, 9, 8310, 23, 8311, 2, 8312, 2, 8314, 3, 8315, 15, 8316, 2, 8317, 15, 8318,
5, 8319, 4, 8320, 4, 8321, 2, 8322, 2, 8323, 2, 8324, 6, 8325, 7, 8326, 4, 8327, 9, 8328, 6,
8329, 1, 8330, 115, 8331, 7, 8332, 7, 8333, 7, 8334, 8, 8335, 9, 8336, 9, 8337, 8, 8338, 8,
8339, 8, 8340, 3, 8341, 2, 8342, 2, 8343, 2, 8345, 10, 8348, 1, 8349, 1, 8352, 6, 8353, 62,
8354, 32, 8355, 2, 8356, 2, 8357, 1, 8358, 14, 8359, 2, 8361, 1, 8362, 1, 8366, 1, 8371, 1,
8373, 1, 8414, 4, 8417, 1, 8423, 9, 8444, 7, 8445, 21, 8446, 2, 8447, 2, 8460, 4, 8461, 3,
8462, 2, 8463, 6, 8464, 2, 8465, 5, 8466, 2, 8467, 4, 8468, 3, 8469, 2, 8470, 4, 8471, 3,
8472, 4, 8473, 12, 8482, 2, 8483, 2, 8484, 3, 8486, 5, 8487, 8, 8518, 2, 8519, 26, 8521, 2,
8534, 1, 8549, 1, 8562, 1, 8563, 1, 8564, 2, 8591, 15, 8592, 7, 8612, 1, 8613, 8, 8614, 1,
8617, 1, 8618, 14, 8633, 4, 8638, 2, 8639, 2, 8640, 1, 8641, 1, 8642, 3, 8645, 7, 8654, 1,
8657, 1, 8658, 1, 8659, 1, 8660, 4, 8661, 1, 8664, 1, 32767, 136, 65535, 70,
};

        // ROUND-83 'loadscreen' IFF-LITERAL canon: the ORIGINAL start + loading ("Go") screen
        // members. Byte-faithfully from UIGraphics.far (tools/extract_uigr.py ->
        // tools/iff-dump/uigr-orig/) and the ORIGINAL resource templates (UIGraphics.far
        // Res_Nbhd.h ids kNbhdBck=5000, kNeighborhoodGo=5600, kNeighborhoodGoBig=5601,
        // kDTGo=5424/5425, kVacationGo=5500-5503, kStudiotownGo=5650-5653, kMagicLandGo=5654-5657;
        // Res_Nbhd.RT paths). Sims 1 FAR1 does not compress, so each member's stored DataLength
        // equals its payload length. IFF-literally the member name + byte length + BMP header
        // dimensions (offset 18/22 little-endian) are IFF data and must mount 1:1.
        public class GoScreenCanon
        {
            public string Name; public int Bytes; public int W; public int H;
            public GoScreenCanon(string name, int bytes, int w, int h) { Name = name; Bytes = bytes; W = w; H = h; }
        }
        public static readonly GoScreenCanon[] GoScreens = new GoScreenCanon[] {
            new GoScreenCanon("Nbhd\\NScreen.BMP", 476066, 800, 600),                        // kNbhdBck (5000) - original start/neighborhood screen
            new GoScreenCanon("Community\\Bus_loadscreen_800x600.bmp", 456692, 800, 600),     // kNeighborhoodGo (5600)
            new GoScreenCanon("Community\\Bus_loadscreen_1024x768.bmp", 748936, 1024, 768),   // kNeighborhoodGoBig (5601)
            new GoScreenCanon("Nbhd\\Bus_loadscreen_800x600.bmp", 456692, 800, 600),         // IFF dup of kNeighborhoodGo
            new GoScreenCanon("Nbhd\\Bus_loadscreen_1024x768.bmp", 748936, 1024, 768),       // IFF dup of kNeighborhoodGoBig
            new GoScreenCanon("Downtown\\Taxi_loadscreen_800x600.bmp", 466556, 800, 600),
            new GoScreenCanon("Downtown\\Taxi_loadscreen_1024x768.bmp", 768874, 1024, 768),
            new GoScreenCanon("Magicland\\magicland_loadscreen_800x600.bmp", 469320, 800, 600),
            new GoScreenCanon("Magicland\\magicland_loadscreen_1024x768.bmp", 776562, 1024, 768),
            new GoScreenCanon("Magicland\\magicland_loadscreen_hole_800x600.bmp", 474900, 800, 600),
            new GoScreenCanon("Magicland\\magicland_loadscreen_hole_1024x768.bmp", 782552, 1024, 768),
            new GoScreenCanon("Studiotown\\Studiotown_loadscreen_800x600.bmp", 465134, 800, 600),
            new GoScreenCanon("Studiotown\\Studiotown_loadscreen_1024x768.bmp", 773708, 1024, 768),
            new GoScreenCanon("Studiotown\\Studiotown_loadscreen_fan_800x600.bmp", 452974, 800, 600),
            new GoScreenCanon("Studiotown\\Studiotown_loadscreen_fan_1024x768.bmp", 762376, 1024, 768),
            new GoScreenCanon("VIsland\\vacation_loadscreen_800x600.bmp", 456118, 800, 600),
            new GoScreenCanon("VIsland\\vacation_loadscreen_1024x768.bmp", 770696, 1024, 768),
            new GoScreenCanon("VIsland\\vacation_loadscreen2_800x600.bmp", 444084, 800, 600),
            new GoScreenCanon("VIsland\\vacation_loadscreen2_1024x768.bmp", 736600, 1024, 768),
        };

        // ROUND-85 'uichrome' IFF-LITERAL canon: the ORIGINAL live-toolbar category buttons +
        // motive bars (CPanel.RT: kMoodBtn=cpanel\Buttons\Mood.bmp, kJobBtn=Job.bmp,
        // kPersonalityBtn=Personality.bmp, kRelationshipBtn=Relationship.bmp, kPeople=people.bmp,
        // kLiveModeGauge=LiveGadget.BMP); motive fills Greenbars.BMP/Redbars.bmp are the 3-slice
        // (9px cap + 9px mid) gauge fill. name+Bytes+WxH from tools/iff-dump/r85/r85-uigr-canon-scan.txt.
        public class ChromeCanon
        {
            public string Name; public int Bytes; public int W; public int H;
            public ChromeCanon(string name, int bytes, int w, int h) { Name = name; Bytes = bytes; W = w; H = h; }
        }
        public static readonly ChromeCanon[] LiveChrome = new ChromeCanon[] {
            new ChromeCanon("cpanel\\Buttons\\Mood.bmp", 5846, 128, 39),
            new ChromeCanon("cpanel\\Buttons\\Job.bmp", 3648, 108, 30),
            new ChromeCanon("cpanel\\Buttons\\Personality.bmp", 3862, 108, 30),
            new ChromeCanon("cpanel\\Buttons\\Relationship.BMP", 4290, 108, 30),
            new ChromeCanon("cpanel\\Buttons\\people.bmp", 7478, 200, 50),
            new ChromeCanon("cpanel\\Greenbars.BMP", 2156, 27, 25),
            new ChromeCanon("cpanel\\Redbars.bmp", 2156, 27, 25),
        };

        // ROUND-86 'uitoolbar' IFF-LITERAL canon: the ORIGINAL live-toolbar chrome (CPanel.RT:
        // kHouseBtn=cpanel\Buttons\House.bmp, kOptions=options.bmp, kPause=pause.bmp,
        // kOptionExit=OptExit.bmp, kObjects=objects.bmp, kLiveModeGauge=cpanel\Backgrounds\LiveGadget.bmp).
        // name+Bytes+WxH from tools/iff-dump/r86/r86-toolbar-canon-scan.txt.
        public static readonly ChromeCanon[] ToolbarChrome = new ChromeCanon[] {
            new ChromeCanon("cpanel\\Buttons\\House.bmp", 3900, 108, 30),
            new ChromeCanon("cpanel\\Buttons\\options.bmp", 2632, 92, 23),
            new ChromeCanon("cpanel\\Buttons\\pause.bmp", 2438, 60, 30),
            new ChromeCanon("cpanel\\Buttons\\OptExit.bmp", 3162, 200, 40),
            new ChromeCanon("cpanel\\Buttons\\objects.bmp", 6008, 180, 47),
            new ChromeCanon("cpanel\\Backgrounds\\LiveGadget.bmp", 6612, 108, 100),
        };
        // ROUND-87 'uicur'/'uiglyph' IFF-LITERAL canon: the ORIGINAL UIGraphics.far .cur cursors (51)
        // and .ffn glyph-tables (45), byte-verbatim (engine-independent raw FAR scan;
        // tools/iff-dump/make_r87_canon.py -> tools/iff-dump/r87/). Each member is pinned by its raw
        // FAR stored name (the ORIGINAL game path, e.g. Shared\cursors\LivePerson.cur) + byte
        // length + sha256. Sims1 FAR1 does not compress, so DataLength == payload length. The IFF-first
        // live mount loads/fingerprints exactly these member bytes.
        public class R87ResourceCanon
        {
            public string Name; public long Bytes; public string Sha256;
            public R87ResourceCanon(string name, long bytes, string sha256) { Name = name; Bytes = bytes; Sha256 = sha256; }
        }
        public static readonly R87ResourceCanon[] R87CursorCanon = new R87ResourceCanon[] {
            new R87ResourceCanon("Shared\\cursors\\ADDPTS.cur", 326, "7f370e92e217bf3cb5cd7d98983dcf73f558e7e0eceab8cb63887e3010658a53"),
            new R87ResourceCanon("Shared\\cursors\\Arrow.cur", 326, "73e6714e659026fb32244fbf161240f441eb79c9e8e37b9562b480d2cca6c4bf"),
            new R87ResourceCanon("Shared\\cursors\\BULLDOZE.cur", 326, "414247cb6dc366c68032033c478419eafff6303f045645ec4ff7d413e32bed6d"),
            new R87ResourceCanon("Shared\\cursors\\DEADEXPL.cur", 326, "1fd2e6b0cd33c51f030bb3c05760b693a0694b5476777600ebb3ffade0f07e76"),
            new R87ResourceCanon("Shared\\cursors\\DOWN.cur", 326, "fcb8f89591ed947aba11af7c11aa1172e1e101f392c119b827cd84a8e1e99561"),
            new R87ResourceCanon("Shared\\cursors\\DOWNLEFT.cur", 326, "758c813b73199dba090c11b9c7641a5c7d961681d12dc35ca0a74f2101a6b7ee"),
            new R87ResourceCanon("Shared\\cursors\\DOWNRIGHT.cur", 326, "74e471da7cfc6368128c15484dd59e0db4312fef16457af6bafef929f57b40d4"),
            new R87ResourceCanon("Shared\\cursors\\EXPLORE.cur", 326, "7768ff14e865d0a85f7673d6c672370b7b190906dca15e93d53d13de86000aa1"),
            new R87ResourceCanon("Shared\\cursors\\HELP.CUR", 518, "f243815541b6b77a91bed8d2408c9d6e56eb99448bf5087411b70d7691da12ca"),
            new R87ResourceCanon("Shared\\cursors\\HOURGLASS.cur", 326, "3466186ec9a32bf6ba39143b2d2947256b30a26b1bb594488322ac801c360b95"),
            new R87ResourceCanon("Shared\\cursors\\LEFT.cur", 326, "bacd594b571309b925caaea39d3d5ea6a5fed7c60f5d7c5ee7c8ab53ee99b1a3"),
            new R87ResourceCanon("Shared\\cursors\\LiveNothing.cur", 326, "5a285dcdce01730f57de1819ed1b18b8727b97ac954ee5af6f179358e338124a"),
            new R87ResourceCanon("Shared\\cursors\\LiveNothingPet.cur", 326, "3ad5a0244dfdd94c9ae4ded334c8a51edf6572045af7e35847d2eef657dfaa46"),
            new R87ResourceCanon("Shared\\cursors\\LiveObjectAvail.cur", 326, "d8c1fb63e7eeb727bd020a28e8093376c82f0707efb90cb5637d8d527bc45b6a"),
            new R87ResourceCanon("Shared\\cursors\\LiveObjectUnavail.cur", 326, "b9fd7faa127e7961235328e9246ec5d278191ac62b271c4cf066fe54d598f8f2"),
            new R87ResourceCanon("Shared\\cursors\\LivePerson.cur", 326, "bdb0bbd1365b209d4bc3a8b7900579b64950c58dd2c50315409a447b2768313b"),
            new R87ResourceCanon("Shared\\cursors\\LivePet.cur", 326, "35de90968b07c6e16d9cfde6490b55707ccf0bf2591a5f19c4e78383e75b2d02"),
            new R87ResourceCanon("Shared\\cursors\\MOVEIN.cur", 326, "96b9acff67b14f5cc61d75e85ae06d9919da6f5438f23e21d7ec52aca5f0ffbf"),
            new R87ResourceCanon("Shared\\cursors\\NODROP.CUR", 326, "72b31aa72e650058a40dbf6bb8b6c1969d4748a0f7b7f4009fd48591d2b514c8"),
            new R87ResourceCanon("Shared\\cursors\\Pan.cur", 326, "618e49f085e983cc8d9152753be5a9fe7f79c796c3d8b80fba911eabf27882b7"),
            new R87ResourceCanon("Shared\\cursors\\RIGHT.cur", 326, "a4aea927bbafc7afc1495f07fcd395fc18561a5dea983a2eb7d81306a9e288c7"),
            new R87ResourceCanon("Shared\\cursors\\RemovePts.cur", 326, "5e654414f293655022f6727560f981a1cd9001f7504ebe4e38e9d71aea4e0b3d"),
            new R87ResourceCanon("Shared\\cursors\\Rezone.cur", 326, "414247cb6dc366c68032033c478419eafff6303f045645ec4ff7d413e32bed6d"),
            new R87ResourceCanon("Shared\\cursors\\Rezone2.cur", 326, "8925e7115b477a4c09eb743bd988f578f134868ed9e97b3bb8b760c20005e1e8"),
            new R87ResourceCanon("Shared\\cursors\\SimsDelete.cur", 326, "e1f13576287ca0c038db699059f53907580639f3343cfa5c9f6b497bcef972a1"),
            new R87ResourceCanon("Shared\\cursors\\SimsFlip.cur", 326, "774db41fbc70b9173c7428f2b86f3eefd27f19aefd471565a9f314338e153f7c"),
            new R87ResourceCanon("Shared\\cursors\\SimsLevel.cur", 326, "aef178b58e8b3320155969b92f21b180ad6db0144c78c101c4c3275fa67d4db6"),
            new R87ResourceCanon("Shared\\cursors\\SimsLower.cur", 326, "4fea0655f4e2e3cbb19bd43fb8114835694d6170f9ad0e0a3ab67cbcd4710fce"),
            new R87ResourceCanon("Shared\\cursors\\SimsMove.cur", 326, "92ae519912ba207cbd8a29f6e058e332482e73f296166b15ead72d25807fc739"),
            new R87ResourceCanon("Shared\\cursors\\SimsPlace.cur", 326, "ffd017e715e9820ce3f04a0327f34c62320906d9ef54bf43119225dc64ebe36d"),
            new R87ResourceCanon("Shared\\cursors\\SimsRaise.cur", 326, "740e2a392c198896821f5f8b8ca4cb1b30c3a295fe56152f0bbf379c8438fc16"),
            new R87ResourceCanon("Shared\\cursors\\SimsRotate.cur", 326, "774db41fbc70b9173c7428f2b86f3eefd27f19aefd471565a9f314338e153f7c"),
            new R87ResourceCanon("Shared\\cursors\\SimsRotateNE.cur", 326, "2d7f6de3cd6f76cb70f11b3b62cd2c0068048ab7e5078e62e83778f59e05e351"),
            new R87ResourceCanon("Shared\\cursors\\SimsRotateNW.cur", 326, "a2b58a68c32cbf04ef184394d4690e64dccc83e63fc549165ff54647d89e4d31"),
            new R87ResourceCanon("Shared\\cursors\\SimsRotateSE.cur", 326, "c1d940e3ef6f8f6e7d8188da4fded28f8f58e1390382d87ab0faf82ec628a012"),
            new R87ResourceCanon("Shared\\cursors\\SimsRotateSW.cur", 326, "4ddfe77b3ca3a8e0a2009497a6dcc0a9d21a90f799cebd62b520142ff41478f4"),
            new R87ResourceCanon("Shared\\cursors\\UP.cur", 326, "17ceaa88e2aeee8323dccd354ba3b47619e00603cdca48cdc038f0ecb0c828fe"),
            new R87ResourceCanon("Shared\\cursors\\UPLEFT.cur", 326, "ead46f33789a4ba39945f7685a627ace198bf8ff71a4a6e924b9bbbf65f3c0f3"),
            new R87ResourceCanon("Shared\\cursors\\UPRIGHT.cur", 326, "f03666e9438aba15476108703c813e0bfc6f059227b3ce4e49269435f56a4d29"),
            new R87ResourceCanon("Shared\\cursors\\balloon.cur", 326, "ff0b24da198049ed1568bd6d25ccae4d0d2f69ff529afed4e3463f8fd88bbe4e"),
            new R87ResourceCanon("Shared\\cursors\\edit.cur", 326, "f913c09929fed25ac9419be4dc73a1412acac4e5b0ad9b5d4dbdb42926037f1d"),
            new R87ResourceCanon("Shared\\cursors\\finger.cur", 326, "8aad6a66080f25fb95578db5db23313557be1735c661a44f57d6a38ffc123aa7"),
            new R87ResourceCanon("Shared\\cursors\\hand.cur", 326, "92ae519912ba207cbd8a29f6e058e332482e73f296166b15ead72d25807fc739"),
            new R87ResourceCanon("Shared\\cursors\\hole.cur", 326, "a3ab08b469f8515bad8dde134a94cc1c4ca16cdce8b1974611d7dcf451f14f05"),
            new R87ResourceCanon("Shared\\cursors\\limo.cur", 326, "7962ce66669711c932363b6097c93b136fdc360fc5fa3e4f38639861f4899998"),
            new R87ResourceCanon("Shared\\cursors\\ship.cur", 326, "3dc6eee77057856a4846869cdcbd5a0daea5e09a9be325fe85dddd43b75d30bd"),
            new R87ResourceCanon("Shared\\cursors\\taxi.cur", 326, "85a537c99bb774d69b5e3d7ee73854abd7b116b21824ac9fb6675176cf19d5ca"),
            new R87ResourceCanon("Shared\\cursors\\tram.cur", 326, "c90c8aff7ca9d644dfbebc72d8af3356a158e15c6d64f95dc7eec1651c23ec93"),
            new R87ResourceCanon("Shared\\cursors\\trolleybus.cur", 326, "bf8d5e40d8d0d53b058bbf9aacdc53b51067b8da809f200ac877435f474f16de"),
            new R87ResourceCanon("Shared\\cursors\\van.cur", 326, "5532181e88752e0c04bbad4a9c55f57404f37702f2e91008a042717424f16d0b"),
            new R87ResourceCanon("Shared\\cursors\\water.cur", 326, "721792b282c6c0ca0182ff5ff87e1f77e60b40de56c7f70ca53e749a9d9a2068"),
        };
        public static readonly R87ResourceCanon[] R87GlyphCanon = new R87ResourceCanon[] {
            new R87ResourceCanon("Fonts\\Polish\\variablesans_07.ffn", 13264, "dde3470df1f01dc3595b40b069905f9ee27ffba277b8ad798c136fffd4725174"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_08.ffn", 14672, "12246d838e0faf54e3e8bd4c33df472137069e6e1c3adba59ad94f3991618261"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_09.ffn", 17104, "1fbfb1e1881e75a7ef9f1f04afb441f446ea4ef185d6ccdad7824e3f318a756b"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_10.ffn", 17744, "e6eb81f0b75dd0fdaa856215be687de071109effe3c13c323eca15a7f5961e00"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_11.ffn", 22352, "c0a47743d5f32cfdd2ff45fd01c00c5cca7b2490e129211d59c46beaaaf4fb69"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_11_bs.ffn", 27472, "bfc01b310c64830df58f586aea1327feef26740e7c74efb534c22b145e8b8ae9"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_11_s.ffn", 25040, "4ef0d675c77a7fe4c0821a571929c3f7bd66ffe425181ab467945cde4d8b0ad8"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_12.ffn", 25424, "fd714229feb75036ec4fc103f9c4b626e8faf9805d4d60655f5d1a23a44d5389"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_12_bs.ffn", 28496, "8d23f18e63198eaa7fb1339dd6def5d1948e2c3f6c6c3b93a22c7e358269b6da"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_14.ffn", 31184, "c238ced52eec685402365977c4eb91c28dc35104e1bfdd827b5f246e6e513277"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_14_bs.ffn", 42448, "f9544f92597553472f8de988ea99fab5eca753b5426bbbf2817761fbf278cfa7"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_16.ffn", 42960, "8f6650493a8e083ed7b3bd8f73fbcd2de782b863d3ee10dad650aac2e2925a3e"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_18.ffn", 44112, "e50a9e6961391270f47ca6ab14a3a3405d756605e50e5fd252cf6419990fb04b"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_20.ffn", 57168, "9b161ca05825805aa4ab938e3c009b609acd96cef7bd9a25867af215ff8b7691"),
            new R87ResourceCanon("Fonts\\Polish\\variablesans_48.ffn", 18328, "57e4648fa00fef90c89ddbe52b76790a0890c3e827e7be8a4408f04289e1b0ac"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_07.ffn", 12624, "edfc989a793ee898c6c948ffbe824eac51d4482dbfd1f67a0ffd217acc951b81"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_08.ffn", 6096, "8c34cfde0bda42f5294063212c1325315899698100ff04b11de87d9fc6cb6e91"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_09.ffn", 6288, "ebd885f8aadb5b51a6c63810488136257bd7e01d98ef5760e850fb425ca56edb"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_10.ffn", 6480, "10fc6835b5df775ec39059219b6bdddd99e6df0942b417198e6415375ac131f5"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_11.ffn", 8336, "1e9a3a5f0a8c6e1a901c991ea72c96a8106f9b8d798eaa57469551514c9845ff"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_11_bs.ffn", 11088, "0992f7e55b5c7622e59e81287f0a3ac06fd98f60cd66308775c18bfcbc870ebe"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_11_s.ffn", 8400, "46b8df095380b3c85538c41b576cc463d31509110c07e67c4aaf9b3a7fdf7f92"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_12.ffn", 8400, "37ea2bb5a43624b7eb1f0925229eb8170b8e90db01c1ef13dc7a026274a895b0"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_12_bs.ffn", 11600, "682e8f61455f0d234cbd8b580bdd5d1bac72602f40c89056097e54194cffcaf0"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_14.ffn", 12240, "5d4f26b7625de11a3661829b39515155ffc4c378a03dc0aad27b48c1261a3ac1"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_14_bs.ffn", 12752, "bef96ab6c1920d521317ad6a2327398b4eedfedbb8478b1314e9a6ccaeb1c313"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_16.ffn", 13136, "1ab3e08c28c1a5a92d1d8c96c4e50c1044dec43170f38d3c96340f178678d080"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_18.ffn", 17744, "643aca0da3660b27202c4327376e7f95dafcb8ac8b826f8ab1249db1c5ebc907"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_20.ffn", 19024, "9c2ee567e792ae0aca4d23de0eba03dc13374965e0aca3d7fc3d63882ff3754a"),
            new R87ResourceCanon("Fonts\\Russian\\variablesans_48.ffn", 90320, "bd7e8f2dbd55e930c3ea552b3f3168825ca334af4e3e7779c8ec1dafed75bb58"),
            new R87ResourceCanon("Fonts\\variablesans_07.ffn", 13136, "aaa09e228e3bd37667bda0239d28bdc159559f6d31cd3c77062ed54efc20b905"),
            new R87ResourceCanon("Fonts\\variablesans_08.ffn", 16208, "9a9b707e34248ce1a3963893961dc23608b810e7745554c79b7cded8b5ef3afb"),
            new R87ResourceCanon("Fonts\\variablesans_09.ffn", 17232, "13bb1ad465554f60a699e52cd4cf4decfdac83e4b778b53a613eddf627fab1a6"),
            new R87ResourceCanon("Fonts\\variablesans_10.ffn", 18000, "15f1b7a4b62652d3302ffcb7fde68de5523ab3f3671c8257985ee665b24ea8fd"),
            new R87ResourceCanon("Fonts\\variablesans_11.ffn", 21968, "8c44d999997375648924da2763dd4cfda9ffbe2775d844bc819eef16d2823e78"),
            new R87ResourceCanon("Fonts\\variablesans_11_bs.ffn", 27088, "e733a889f031aa358fa95df91b6b31a90fff41c4bbaac8bda9aab4e9a337366a"),
            new R87ResourceCanon("Fonts\\variablesans_11_s.ffn", 24784, "3e64aad37c74c656655151bb0942cf293c6ec5394ab2844c9f5242fd84d309a1"),
            new R87ResourceCanon("Fonts\\variablesans_12.ffn", 25552, "f584b9de66c9e588f541d7d1643acf1f2e91586fdc8636fc408fbfe603117a1c"),
            new R87ResourceCanon("Fonts\\variablesans_12_bs.ffn", 28624, "dcf191ccc4de340104fcffa662eecb0cf19011e772c7c107580b465b8413aa43"),
            new R87ResourceCanon("Fonts\\variablesans_14.ffn", 31184, "9075d4b9422e804b9f8614601843f1e8f94fffb05566ee811b299c5dc991efcc"),
            new R87ResourceCanon("Fonts\\variablesans_14_bs.ffn", 42448, "3dabac18b8d264577b1857a71a0691fccaefaa555b54d99521acea504c09bbb0"),
            new R87ResourceCanon("Fonts\\variablesans_16.ffn", 42960, "9b2237b66ad8d159301aaee0413ed85bd0bfe436c4d412af247fe868253fded4"),
            new R87ResourceCanon("Fonts\\variablesans_18.ffn", 44112, "2ff1f4dd93343fcdca8f0c7d28102f5d1e8dda01bbf3731cc9934ce1ee058fd0"),
            new R87ResourceCanon("Fonts\\variablesans_20.ffn", 57168, "99148fd556073a0c91268ab827e26fd4188dbf7b0d253359cd9d128131788fc3"),
            new R87ResourceCanon("Fonts\\variablesans_48.ffn", 18328, "57e4648fa00fef90c89ddbe52b76790a0890c3e827e7be8a4408f04289e1b0ac"),
        };        // ROUND-88 'uiboot'/'uilogo'/'uianim' IFF-LITERAL canon: the ORIGINAL loading-screen
        // LOGO + ANIMATION families. Member lists are parsed from the ORIGINAL resource
        // templates (Res_Other.RT: kSimsLogo id 9003 -> Other\setup.bmp + 17 language
        // variants = the original full-screen boot/logo screen, plumbob + wordmark art baked
        // in; Res_Nbhd.RT: kNghSimsLogoEn/SP/FR/GR/JP + kDowntownCredits + kStudiotownCredits
        // logo stamps; and the original animated frame families - Unleashed waves + NonUS,
        // TS1.0 frame-deltas + Loc, Downtown/Magicland/Vacation water, Magicland fog + balloon,
        // Studiotown car). The templates themselves are UIGraphics.far members, extracted
        // byte-verbatim to tools/iff-dump/r88/templates/ by tools/iff-dump/make_r88_canon.py
        // (engine-independent FAR1 manifest scan; nothing here reads engine code).
        // Note: the original boot screen has NO separate progress-bar or logo-control asset
        // (no {BITMAP,RLEBMP} entry exists for one) - the original bar is engine-drawn, so
        // the live mount draws an engine bar over the original screen.
        public class R88ResourceCanon
        {
            public string Name; public int Bytes; public string Sha256; public int W; public int H;
            public R88ResourceCanon(string name, int bytes, string sha256, int w, int h) { Name = name; Bytes = bytes; Sha256 = sha256; W = w; H = h; }
        }

        // R88 IFF-LITERAL canon: kSimsLogo (Res_Other.RT id 9003) - the ORIGINAL full-screen boot/logo
        // screen, Other\setup.bmp + 18 language variants (18), byte-verbatim.
        // (raw FAR stored name, byte length, sha256, BMP/TGA dims) - IFF-literal mount keys.
        public static readonly R88ResourceCanon[] R88BootCanon = new R88ResourceCanon[] {
            new R88ResourceCanon("Other\\setup.bmp", 1440056, "8ecf950019fb86bed4be7d02e1e72ff2217e8ea1d5b44da6c6a0deec2d934ae6", 800, 600),
            new R88ResourceCanon("Other\\setupuk.bmp", 1440056, "2228004ed8d120efa4bf79ace40bef4f635481a88bf2f101f67b056fb08577f6", 800, 600),
            new R88ResourceCanon("Other\\Setupfr.BMP", 1440056, "39d76db0400ea4942a5e52cd1113eb63d2631773e0d1c540c41901ff6371d042", 800, 600),
            new R88ResourceCanon("Other\\Setupde.bmp", 1440056, "86a5f6bb72fe776e8e8d8a64f96a41f28085c422a9b25ddc0a5ebc69b091d83f", 800, 600),
            new R88ResourceCanon("Other\\SetupDu.bmp", 1440056, "c368501f711a8de585dcac5832b713d33406a3e2c6c1e123473ab46b49f7ac69", 800, 600),
            new R88ResourceCanon("Other\\SetupSw.bmp", 1440056, "2228004ed8d120efa4bf79ace40bef4f635481a88bf2f101f67b056fb08577f6", 800, 600),
            new R88ResourceCanon("Other\\SetupIt.bmp", 1440056, "4df7e624fd93b1f98a68dc37ef55de8f48295bdb7053d8faeeb6e6e4fb5704cf", 800, 600),
            new R88ResourceCanon("Other\\Setupjp.bmp", 1440056, "7b5a96b2f5553d5fb3c40ba52ce0b762f632bf2dbfbb14532515dd0e6e40a85b", 800, 600),
            new R88ResourceCanon("Other\\SetupTh.bmp", 1440056, "2dab7dcefc1966b4410e0402fbe7cab2bcae021dba033ce35cf521a84905b678", 800, 600),
            new R88ResourceCanon("Other\\SetupTC.bmp", 1440056, "d570190cd513524fce507ecd9c9431603866d18808d4f29f7f06eb85593249a9", 800, 600),
            new R88ResourceCanon("Other\\Setupsp.bmp", 1440056, "a012b42a038af9b79826ef8db43bc9f73ca3d93ce9ebca912ce013a2568b95b1", 800, 600),
            new R88ResourceCanon("Other\\SetupPg.bmp", 1440056, "ed20fb0205413ddaf29128b8b7f498ceac6f8f062304eb55517e2591aa95e7bd", 800, 600),
            new R88ResourceCanon("Other\\SetupSC.bmp", 1440056, "7f644b6d7ed8ee268529ced1157ad32f35048cef4526fe745d1d5ab3f687bc17", 800, 600),
            new R88ResourceCanon("Other\\SetupPo.bmp", 1440056, "e7322d5cfa3b233f6ea34e05afe003adc1c971de6547af36a86686ca6ac4e807", 800, 600),
            new R88ResourceCanon("Other\\SetupKo.bmp", 1440056, "e58a62348c70b7980c37e2fd59529f41a1d01c1cdb55843d2b6e05fe5ae9844f", 800, 600),
            new R88ResourceCanon("Other\\SetupDN.bmp", 1440056, "2228004ed8d120efa4bf79ace40bef4f635481a88bf2f101f67b056fb08577f6", 800, 600),
            new R88ResourceCanon("Other\\SetupNO.bmp", 1440056, "2228004ed8d120efa4bf79ace40bef4f635481a88bf2f101f67b056fb08577f6", 800, 600),
            new R88ResourceCanon("Other\\SetupFI.bmp", 1440056, "bb5fd1117141e5728a1e2f7955d788f9fcda1baf549696078b42b57e91a95f78", 800, 600),
        };
        // R88 IFF-LITERAL canon: the ORIGINAL 'The Sims' logo stamps - kNghSimsLogoEn/SP/FR/GR/JP +
        // kDowntownCredits + kStudiotownCredits (7 members), byte-verbatim.
        // (raw FAR stored name, byte length, sha256, BMP/TGA dims) - IFF-literal mount keys.
        public static readonly R88ResourceCanon[] R88LogoCanon = new R88ResourceCanon[] {
            new R88ResourceCanon("NghUI\\TheSimsLogo.bmp", 2934, "ebae562f5550abe7b5767b408b2cca8c59884807bda1525d73173ff11ec6d7ba", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_sp.bmp", 2888, "7b34f7129b401544a1c05963595adbf6a792a54086342b26f2013ef24e735b88", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_fr.bmp", 2930, "3a642e8cc38f0e37036f00dcf66875b7d8b6d0a8926df5fd5ea9f0ca7c204ded", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_gr.bmp", 2940, "7d08dae35b5852e475bc08a3601250f1cae913dd079d239d58bba81c883d1222", 70, 52),
            new R88ResourceCanon("NghUI\\TheSimsLogo_jp.bmp", 3594, "b14c795aab984407e5ee9621eb583406402d5f6fa66696bff7db5ae61daef932", 99, 52),
            new R88ResourceCanon("Downtown\\TheSimsLogo.bmp", 2934, "ebae562f5550abe7b5767b408b2cca8c59884807bda1525d73173ff11ec6d7ba", 70, 52),
            new R88ResourceCanon("Studiotown\\TheSimsLogo.bmp", 2934, "ebae562f5550abe7b5767b408b2cca8c59884807bda1525d73173ff11ec6d7ba", 70, 52),
        };
        // R88 IFF-LITERAL canon: the ORIGINAL animated frame families from Res_Nbhd.RT - Unleashed
        // waves + NonUS, TS1.0 frame-deltas + Loc, Downtown/Magicland/Vacation water, Magicland fog +
        // balloon, Studiotown car (74 frames), byte-verbatim.
        // (raw FAR stored name, byte length, sha256, BMP/TGA dims) - IFF-literal mount keys.
        public static readonly R88ResourceCanon[] R88AnimCanon = new R88ResourceCanon[] {
            new R88ResourceCanon("Community\\NScreen_unleashed_waves001.bmp", 52396, "739cca3e6697088e19999a5e285edd3111a3836721b125b01c0c7b5ac6380149", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves002.bmp", 52218, "b1e8a6c7c4c38d080dfd50646a18226b80f7e7a10b78f54f0f98aa2393629f0f", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves003.bmp", 51888, "2f9e663019e7c80ceb1347b8e77fa15a8e016aceabbf5bbef7d280c1f38ccc57", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves004.bmp", 52462, "5230b3b91bb54fe82a6ef09435ed39bdac31ee5a177922ea9c0ff410ffd62c61", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves005.bmp", 52180, "5196f3c39f54aa3f56998ae65cbf0a4903cab405cb980133cfbba26d2972049a", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves006.bmp", 52326, "9c9181d4fd460506d529d254175b19b935714048fd60aeee16cdcbea3fa78e04", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves001_nonus.bmp", 52396, "739cca3e6697088e19999a5e285edd3111a3836721b125b01c0c7b5ac6380149", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves002_nonus.bmp", 52218, "b1e8a6c7c4c38d080dfd50646a18226b80f7e7a10b78f54f0f98aa2393629f0f", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves003_nonus.bmp", 51888, "2f9e663019e7c80ceb1347b8e77fa15a8e016aceabbf5bbef7d280c1f38ccc57", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves004_nonus.bmp", 52462, "5230b3b91bb54fe82a6ef09435ed39bdac31ee5a177922ea9c0ff410ffd62c61", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves005_nonus.bmp", 52180, "5196f3c39f54aa3f56998ae65cbf0a4903cab405cb980133cfbba26d2972049a", 800, 600),
            new R88ResourceCanon("Community\\NScreen_unleashed_waves006_nonus.bmp", 52326, "9c9181d4fd460506d529d254175b19b935714048fd60aeee16cdcbea3fa78e04", 800, 600),
            new R88ResourceCanon("Nbhd\\DiffN1-N2_8.bmp", 44874, "b211bfd4bcc885ac2bdd3c8c4869a5ce135840ef158b087d8d61246e6a7b60fb", 800, 600),
            new R88ResourceCanon("Nbhd\\DiffN1-N3_8.bmp", 44900, "8d25c43b721a4642bf0c90d12f5b5f1829d54ca180f574837a50c0a6d47d5da2", 800, 600),
            new R88ResourceCanon("Nbhd\\DiffN1-N4_8.bmp", 44978, "16fb5b21adabe93c153343808dd098b0b8e187117f472d3833bef1cbb515f6a1", 800, 600),
            new R88ResourceCanon("Nbhd\\DiffN1-N2_Loc_8.bmp", 43592, "68a1bddf00c85a67cef408aedccd231bfde2187474fa12f32604932eac1541e9", 800, 600),
            new R88ResourceCanon("Nbhd\\DiffN1-N3_Loc_8.bmp", 42938, "a3d37771717e2cbfb5816e4d773eea0910cffd218ff02d2fa25dc68a6452651f", 800, 600),
            new R88ResourceCanon("Nbhd\\DiffN1-N4_Loc_8.bmp", 43838, "70909b3c25d10ebd23744fe85d506c774793fc53f330bad511e0d139f03c2f68", 800, 600),
            new R88ResourceCanon("Downtown\\dscreen00.bmp", 59244, "25a84fc7cb70af0b63d932010f8ab33d00757bbb0d436228ffd18fa6daecb42b", 800, 600),
            new R88ResourceCanon("Downtown\\dscreen01.bmp", 79658, "cbf7194c6b810eee1123d417750ac51acce27c060b522ddbb70a53c9701f155b", 800, 600),
            new R88ResourceCanon("Downtown\\dscreen02.bmp", 85640, "7eb3bafa4589ed80b6ca9b2a5c167659718b6b16333f4c15313981b9d96880a7", 800, 600),
            new R88ResourceCanon("Downtown\\dscreen03.bmp", 87420, "77765e2185d0edf879fc728d4f68ca2fa195c2db2802b3fda2827bfe0240e58e", 800, 600),
            new R88ResourceCanon("Downtown\\dscreen04.bmp", 88086, "8afcaa6ff47393e3b6602b5058c7dc30d7946c10fab433d1fe050da150740852", 800, 600),
            new R88ResourceCanon("Downtown\\dscreen05.bmp", 2578, "186700be95447162f7738b97349bf61f065891618b552e0a2ef24f735cbab496", 400, 250),
            new R88ResourceCanon("Magicland\\DScreen_waves1.bmp", 16924, "0ae612d11da10ed2d0180a2ca6ea532d5f53ec8a1f934b98a2dd3a7176c189cb", 647, 362),
            new R88ResourceCanon("Magicland\\DScreen_waves2.bmp", 16928, "284430df8ef78b390c617469695da3d66f8b20e31506ca1fb8542f49838e3e98", 647, 362),
            new R88ResourceCanon("Magicland\\DScreen_waves3.bmp", 16838, "46db32de127f8ddccd4f503ad90948c16c1fc6b8818e5cfec1e2415ab3b73c7e", 647, 362),
            new R88ResourceCanon("Magicland\\DScreen_waves4.bmp", 16882, "58cd9b55953204f2db1539b8d3b924109873514258e77d673d7e6e1f43948466", 647, 362),
            new R88ResourceCanon("Magicland\\DScreen_waves5.bmp", 16908, "c736755872d4e6fd6b8bfad03acb5a8267c3843b671f20dd049346d05bf2f000", 647, 362),
            new R88ResourceCanon("Magicland\\DScreen_waves6.bmp", 16940, "a1ad030a7e22182fdbacbd81b1e90047856edd8864a322b544e9dff00b84b4f8", 647, 362),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car0.bmp", 1402, "8270b7f5f63d4defc38a9d6dd0e68b59fb36905ed2e6a52345f5a492052cfea9", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car1.bmp", 1402, "8270b7f5f63d4defc38a9d6dd0e68b59fb36905ed2e6a52345f5a492052cfea9", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car2.bmp", 1410, "d7ed1244f36db53d7153877376d567df1f023cacc27ac75e79f677ce6ed5e1e3", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car3.bmp", 1410, "d7ed1244f36db53d7153877376d567df1f023cacc27ac75e79f677ce6ed5e1e3", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car4.bmp", 1410, "eeec91b6624fbe87ea553255d27faa0970eeb4b2fd4a55beb6a79b45313c6dde", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car5.bmp", 1410, "eeec91b6624fbe87ea553255d27faa0970eeb4b2fd4a55beb6a79b45313c6dde", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car6.bmp", 1410, "37340482947308fb556a6bd9889f623a32048e4ea3bb1af9244e9d0658372885", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car7.bmp", 1410, "37340482947308fb556a6bd9889f623a32048e4ea3bb1af9244e9d0658372885", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car8.bmp", 1404, "3f1634d3984e87c080bbef53f32513faca8985cba61d932f2f8011ebd80a6798", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car9.bmp", 1404, "3f1634d3984e87c080bbef53f32513faca8985cba61d932f2f8011ebd80a6798", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car10.bmp", 1236, "fbd962003539ca59323f1d9360845ffb97fedda5efefdba4c7dc881afaa761d6", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car11.bmp", 1236, "fbd962003539ca59323f1d9360845ffb97fedda5efefdba4c7dc881afaa761d6", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car12.bmp", 1362, "20657b6bb929d9ccadf71c8c5718b0ec41c155d4ebf435071a9739dc44e2f819", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car13.bmp", 1380, "22a74c99a16f20bc76a7fdd77c33650216338d67294769b6d150c4ab16829bad", 24, 15),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car14.bmp", 1448, "166cc3babd84664fcebfec57841588ea78d3e0a26bdbf86dc7c1188d88d9566f", 26, 19),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car15.bmp", 1404, "13f068d4ec5b08a9765a0ff2e0e49b0527e8810797b812c9db42b3018b271904", 26, 19),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car16.bmp", 1440, "588205a7ec7cbaab439f0a69409417ad145cc2e922bf013d90e8b8e2aed02acf", 26, 19),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car17.bmp", 1444, "cbba3d1de7bc0b5c2640e3b19d84c25898ada3aac5fa4e5062368d60ec88742d", 26, 19),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car18.bmp", 1556, "b29d38bcab0d20dad8ca2cff8c4e5f313a55443b9b51b1a8ba47fd4fa34d7e9a", 26, 19),
            new R88ResourceCanon("Studiotown\\Nhood_superstar_car19.bmp", 1528, "e919bf7aebeca5b66afe2c44760f4a89699e046527a744b9b69f16ce5c94fe38", 26, 19),
            new R88ResourceCanon("Magicland\\Nhood_balloon1.bmp", 3116, "1f57cba9229dd28e8d1c0e0929845e9d8040154d0366ce79c86c7053f920c449", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon2.bmp", 2800, "887747d4e57016cae23293e3b38ac54866163b1e3c6b8628f54f150df890ab4e", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon3.bmp", 2774, "f5f0dfcabbf690d03ab8c181908ef58201733cbcca07b919a098e68df62d6688", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon4.bmp", 2756, "96de6298fc916465a05c366bc9f5bb22e02c1c138ed126af3e751b871e77a03f", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon5.bmp", 3104, "66079e39aa8867e4706667a0447f3efefb9f56525bf328e994eddcce0b94e289", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon6.bmp", 3400, "19d1cddcf0f3f9ae6419b5522a4154b5f74ccf203eeb9d93aec490f0bcea6b16", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon7.bmp", 3500, "085109e8f68c78f141ee958f8c04292a9469ab6d28eb51039d351674758811c3", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon8.bmp", 3480, "17d61e8ceef3cad415ca30cd095ee1bfdad39def87596dfe31d1a3e709fa10ee", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon9.bmp", 3248, "4e7af25db3fd3e2c31d2b3b7a818ad98ce5302a9e355984743eccf5d0e92d3ac", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon10.bmp", 2916, "a035d6d0930f879039a18847a2fd077c68e96be62c96e41a87c323a78f6ac3af", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon11.bmp", 2834, "a91e0d6fd3dd12983fd5fd1fc1f5882374df287dabae39f8bcdc4ca60fe5aad7", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon12.bmp", 2768, "a8dd5a1e1e2ef777e690602497fdba5838ec609f5b9a9537b1663760f24f5675", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon13.bmp", 3104, "87c5d292e2f4b70f3a727972186fe3336c76d1b0aaf9822a555b76b4773e638e", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon14.bmp", 3396, "8099275c00ef5417e08331902f99e9a4083405846628e3ed73ea97a94f1d6948", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon15.bmp", 3826, "68686538be12076d4b8d7e48a90c3c1c8f4400b0062f03659526a59b20d4220c", 64, 64),
            new R88ResourceCanon("Magicland\\Nhood_balloon16.bmp", 3464, "b3ae63d0f62b7204106face0225cdcdaaade47a156bbf48af0820b4494075695", 64, 64),
            new R88ResourceCanon("Magicland\\DScreen_mistA.tga", 138523, "3d4736a90711ef0f221e390cf954f1bec4cbb239a4fb51e861cc32d0203948ba", 224, 154),
            new R88ResourceCanon("Magicland\\DScreen_mistB.tga", 138028, "bfac1336fdfdddcbbe9d7abfd9c4a9566f7684b0e950d74341ab371bb7e32b5f", 224, 154),
            new R88ResourceCanon("Magicland\\DScreen_mistC.tga", 138028, "b0afe2fc0bdf695f0a303c1894ed2fbdc2a3b93b23db56a56e02bf90811c0604", 224, 154),
            new R88ResourceCanon("VIsland\\visland_waves001.bmp", 13208, "ae27bf2f6c038f3d9c9331496bb5d4a272987dec3fb62e96c9f60fbee525e2f1", 800, 600),
            new R88ResourceCanon("VIsland\\visland_waves002.bmp", 20264, "737a39302902d6df38ac8bf3194e7f1346d56358e033430d091c0df6bb2af309", 800, 600),
            new R88ResourceCanon("VIsland\\visland_waves003.bmp", 25210, "84f512be1d4e7f190885e3620eef5ed704d1a3b950f9d421a0c6f0938527cf19", 800, 600),
            new R88ResourceCanon("VIsland\\visland_waves004.bmp", 30276, "799a58b377c4cba67df8fe8226b4d937afe3ec3a8b2a787a9a160026c55ecb46", 800, 600),
            new R88ResourceCanon("VIsland\\visland_waves005.bmp", 30260, "da77ccb96934798fa6218da006a0c26757d03aaf34b3fa7db98eeffa235664e0", 800, 600),
        };

        // ROUND-89 'uinbhd' IFF-LITERAL canon: the ORIGINAL neighborhood SCREEN members from
        // Res_Nbhd.RT (parsed from the FAR by tools/iff-dump/make_r89_canon.py, byte-verbatim):
        // the six screen backdrops (kNbhdBck/_UL, kDowntown/kStudiotown/kMagiclandBackground,
        // kVacationIsland), the vacation sub-layers (kNghVacationPort/Trees) and the nessie
        // cameo family kNess1-3 (RT-declared under Community/, the Old-Town river). The cycled
        // frame families themselves are pinned by R88AnimCanon; R89 completes their LIVE cycling
        // (UINeighbourhoodSelectionPanel + CheckUINeighborhood). Rows reuse the R88 row shape.
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
    }
}
