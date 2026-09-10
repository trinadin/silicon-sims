using FSO.Client;
using FSO.LotView;
using FSO.UI;
using Simitone.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simitone.Windows
{
    public class GameStartProxy
    {
        public void Start(bool useDX)
        {
            GameFacade.DirectX = useDX;
            World.DirectX = useDX;
            SimitoneGame game = new SimitoneGame();

            Simitone.Client.GameLog.Write("exit-probe: before Run");
            game.Run();
            Simitone.Client.GameLog.Write("exit-probe: after Run");
            game.Dispose();
            Simitone.Client.GameLog.Write("exit-probe: after Dispose");
        }
    }
}
