using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.UI
{
    public class ReadingScrollbarTests
    {
        private GameObject _root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scrollbar_RemainsVisibleAtRestAndHidesWhenCopyFits()
        {
            ScrollRect scroll = CreateScroll(600f);
            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            yield return null;
            yield return null;
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf);

            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            yield return null;
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf,
                "Reading to the bottom must not hide the overflowing scrollbar.");

            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, 100f);
            yield return null;
            yield return null;
            Assert.IsFalse(scroll.verticalScrollbar.gameObject.activeSelf);

            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, 600f);
            yield return null;
            yield return null;
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator RepeatedSetup_PreservesReadingPositionAndSeparateScrollbarLane()
        {
            ScrollRect scroll = CreateScroll(600f);
            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            yield return null;
            yield return null;
            scroll.verticalNormalizedPosition = 0.5f;
            yield return null;
            yield return null;
            Scrollbar scrollbar = scroll.verticalScrollbar;
            float readingPosition = scroll.verticalNormalizedPosition;
            float horizontalInset = scroll.content.offsetMax.x;
            int children = scroll.viewport.childCount;

            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            yield return null;
            yield return null;

            Assert.AreSame(scrollbar, scroll.verticalScrollbar);
            Assert.AreEqual(horizontalInset, scroll.content.offsetMax.x);
            Assert.AreEqual(readingPosition, scroll.verticalNormalizedPosition, 0.001f);
            Assert.AreEqual(children, scroll.viewport.childCount);
            Assert.AreSame(scrollbar.handleRect.GetComponent<Image>(), scrollbar.targetGraphic);
            Assert.AreEqual(Scrollbar.Direction.BottomToTop, scrollbar.direction);
            Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, scroll.content);
            Bounds trackBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, scrollbar.transform);
            Assert.Less(contentBounds.max.x, trackBounds.min.x);
        }

        private ScrollRect CreateScroll(float height)
        {
            _root = new GameObject("Reading scrollbar test", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 1920f);
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect));
            viewport.transform.SetParent(_root.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.sizeDelta = new Vector2(200f, 200f);
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, height);
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }
    }
}
