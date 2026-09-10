// R88 IFF-LITERAL canon: kSimsLogo (Res_Other.RT id 9003) - the ORIGINAL full-screen boot/logo
// screen, Other\setup.bmp + 18 language variants (18), byte-verbatim.
// (raw FAR stored name, byte length, sha256, BMP/TGA dims) - IFF-literal mount keys.
using System;

public class R88ResourceCanon
{
    public string Name; public int Bytes; public string Sha256; public int W; public int H;
    public R88ResourceCanon(string name, int bytes, string sha256, int w, int h) { Name = name; Bytes = bytes; Sha256 = sha256; W = w; H = h; }
}

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
