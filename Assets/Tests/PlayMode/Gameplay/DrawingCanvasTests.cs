using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class DrawingCanvasTests
    {
        private GameObject _cameraObject;
        private GameObject _canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);

            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
        }

        [UnityTest]
        public IEnumerator ClearCanvas_DoesNotDestroyStrokeStartedAfterClearRequest()
        {
            _cameraObject = new GameObject("Main Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();

            _canvasObject = new GameObject("DrawingCanvas");
            DrawingCanvas canvas = _canvasObject.AddComponent<DrawingCanvas>();
            GlyphBadgePlayModeTestHelpers.SetPrivateField(canvas, "_clearDelaySeconds", 0.05f);

            canvas.BeginStroke();
            canvas.EndStroke();
            canvas.ClearCanvas();

            canvas.BeginStroke();
            Assert.AreEqual(2, _canvasObject.transform.childCount,
                "The new stroke should coexist with the pending clear until the clear delay elapses.");

            yield return new WaitForSeconds(0.075f);
            yield return null;

            Assert.AreEqual(1, _canvasObject.transform.childCount,
                "A delayed clear should only remove strokes that existed when ClearCanvas was requested.");
        }
    }
}
