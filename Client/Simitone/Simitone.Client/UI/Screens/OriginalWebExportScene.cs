using FSO.Common.Rendering.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Screens
{
    /// <summary>
    /// ENG-27/AUD-18: the house-render capture hook for the web exporter —
    /// the same next-frame backbuffer readback discipline as R190's snapshot
    /// scene (draws nothing; appended after the lot World so the backbuffer
    /// holds the finished view with no UI painted over it). AUD-18/R2: the
    /// native writes FOUR renders (exterior + floor1 + floor2, ExportHTML
    /// tail); the exporter drives a multi-frame state machine here — each
    /// capture is delivered to OnHouseCaptured, which flips World.State.Level
    /// for the next shot; the world redraws on the following frame and this
    /// scene captures that view (one frame per shot, the player's level
    /// restored at the end). The native renders the floors offscreen on a
    /// HouseViewer; capturing the live view per floor is the disclosed port
    /// necessity.
    /// </summary>
    public class OriginalWebExportScene : _3DAbstract
    {
        public static int Captures;
        public static int CaptureFailures;

        public OriginalWebExportScene(GraphicsDevice device) : base(device) { }

        public override void Update(UpdateState state) { }

        public override void Draw(GraphicsDevice device)
        {
            // stay armed while the exporter's multi-shot queue runs
            // (HouseCaptureRequest remains set until the last floor shot)
            if (Simitone.Client.UI.Model.OriginalWebExporter.HouseCaptureRequest == null) return;
            Texture2D captured = null;
            try
            {
                var vp = device.Viewport;
                var data = new Microsoft.Xna.Framework.Color[vp.Width * vp.Height];
                device.GetBackBufferData((Microsoft.Xna.Framework.Rectangle?)new Microsoft.Xna.Framework.Rectangle(0, 0, vp.Width, vp.Height), data, 0, data.Length);
                captured = new Texture2D(device, vp.Width, vp.Height);
                captured.SetData(data);
                Captures++;
            }
            catch (Exception e)
            {
                CaptureFailures++;
                Simitone.Client.GameLog.Write("webexport: capture EXC " + e.GetType().Name + " " + e.Message);
            }
            finally
            {
                // the exporter's OnHouseCaptured owns the texture from here
                // (and disposes it after writing the JPEGs) — including on failure.
                Simitone.Client.UI.Model.OriginalWebExporter.OnHouseCaptured(captured);
            }
        }

        public override void DeviceReset(GraphicsDevice device) { }
        public override void Add(_3DComponent item) { }
        public override List<_3DComponent> GetElements() => new List<_3DComponent>();
    }
}
