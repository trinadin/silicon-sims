// R87 IFF-LITERAL canon: original UIGraphics.far .cur cursors (51), byte-verbatim.
// (raw FAR stored name, byte length, sha256) = the ORIGINAL game cursor-path member bytes.
// IFF-first live mount must load exactly these member bytes. Text render-style residual unchanged.
using System;

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
            new R87ResourceCanon("Shared\\cursors\\water.cur", 326, "721792b282c6c0ca0182ff5ff87e1f77e60b40de56c7f70ca53e749a9d9a2068"),        };
