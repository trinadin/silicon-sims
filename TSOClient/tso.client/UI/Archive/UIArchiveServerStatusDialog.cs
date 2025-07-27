using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Server.Embedded;
using System;

namespace FSO.Client.UI.Archive
{
    internal class UIArchiveServerStatusDialog : UIDialog
    {
        private readonly UILabel InfoText;
        private bool WaitStart;
        private readonly Action OnComplete;
        private readonly EmbeddedServer Server;
        private readonly UIProgressBar ProgressBar;

        public UIArchiveServerStatusDialog(bool waitStart, EmbeddedServer server, Action onComplete) : base(UIDialogStyle.Standard, false)
        {
            WaitStart = waitStart;
            OnComplete = onComplete;
            Server = server;
            Caption = "Archive Server";

            Add(InfoText = new UILabel()
            {
                Caption = waitStart ? "Starting archive server. Please wait..." : "Safely shutting down archive server before closing.",
                Position = new Microsoft.Xna.Framework.Vector2(20, 45),
                Size = new Microsoft.Xna.Framework.Vector2(200, 50),
                Wrapped = true,
            });

            int ySize = 50 + 70;

            if (waitStart)
            {
                ySize += 37;

                Add(ProgressBar = new UIProgressBar()
                {
                    Position = new Microsoft.Xna.Framework.Vector2(20, 105),
                    Size = new Microsoft.Xna.Framework.Vector2(200, 27)
                });
            }

            SetSize(200 + 40, ySize);

            if (!WaitStart)
            {
                Server.Shutdown().ContinueWith((t) =>
                {
                    GameThread.NextUpdate((state) =>
                    {
                        GameFacade.Kill();
                    });
                });
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);

            if (WaitStart)
            {
                if (Server.ReadyPercent != ProgressBar.Value)
                {
                    ProgressBar.Value = Server.ReadyPercent;
                }

                if (Server.Ready && OnComplete != null)
                {
                    OnComplete();
                }
            }
        }
    }
}
