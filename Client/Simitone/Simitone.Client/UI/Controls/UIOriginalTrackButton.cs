using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Controls
{
    // R132: the ORIGINAL follow-Sim crosshair (kTrackingTarget 4601 ->
    // cpanel\People\TrackingTarget.bmp, 45x45). Engine decode:
    //   * cWinPeople::Init 0x291138 creates the gadget once (li r3,4601
    //     into the 0x3b6190 factory);
    //   * cWinPeople::TrackPerson 0x28bf00-0x28c054 DETACHES any existing
    //     tracking gadget at this+580 (0x509d50 teardown), then — only when
    //     the person matches the global current-person and is non-null —
    //     re-attaches it over that person's portrait slot
    //     (gadget = this->240[personIndex], parented at this+576);
    //   * cWinPeople::StopTracking 0x288740-0x288798 is the pure teardown
    //     (same 0x509d50 + slot clear).
    // The camera-follow itself lives in the engine's world layer; the port's
    // equivalent is WorldState.ScrollAnchor (World.Update re-centers on it
    // every frame), which manual camera input already cancels — the same
    // stop-on-drag behavior as the original. This button carries the
    // crosshair's active state; the port draws it DIMMED when inactive and
    // full-bright while tracking (disclosed interpretation — the engine
    // mounts/dismounts the crosshair over a portrait instead).
    public class UIOriginalTrackButton : UIElasticButton
    {
        public bool TrackActive;

        public UIOriginalTrackButton(Texture2D tex) : base(tex) { }

        public override void Draw(UISpriteBatch SBatch)
        {
            if (!Visible) return;
            DrawLocalTexture(SBatch, Texture, null, new Vector2(Texture.Width, Texture.Height) / -2,
                Vector2.One, Color.White * (Disabled ? 0.5f : (TrackActive ? 1f : 0.35f)));
        }
    }
}
