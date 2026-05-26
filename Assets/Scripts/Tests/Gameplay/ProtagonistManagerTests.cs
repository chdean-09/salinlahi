using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Tests.Gameplay
{
    public class ProtagonistManagerTests
    {
        private ProtagonistManager _manager;

        [SetUp]
        public void Setup()
        {
            // Clean up any existing instances
            if (ProtagonistManager.Instance != null)
            {
                Object.Destroy(ProtagonistManager.Instance.gameObject);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                Object.Destroy(_manager.gameObject);
            }
        }

        [Test]
        public void ProtagonistManager_CreatesSingletonInstance()
        {
            // Arrange
            var gameObject = new GameObject("TestManager");
            _manager = gameObject.AddComponent<ProtagonistManager>();

            // Act
            _manager.Awake();

            // Assert
            Assert.IsNotNull(ProtagonistManager.Instance);
            Assert.AreEqual(_manager, ProtagonistManager.Instance);
        }

        [Test]
        public void EnsureProtagonist_CreatesProtagonistTransform()
        {
            // Arrange
            var gameObject = new GameObject("TestManager");
            _manager = gameObject.AddComponent<ProtagonistManager>();
            
            // Create a simple prefab for testing
            var prefab = new GameObject("TestProtagonist");
            _manager.GetType().GetField("_protagonistPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_manager, prefab);

            Vector3 targetPos = Vector3.zero;

            // Act
            _manager.EnsureProtagonist(targetPos);

            // Assert
            Assert.IsNotNull(_manager.ProtagonistTransform);
            
            // Cleanup
            Object.Destroy(prefab);
        }

        [Test]
        public void EnsureProtagonist_DoesNotCreateIfAlreadyExists()
        {
            // Arrange
            var gameObject = new GameObject("TestManager");
            _manager = gameObject.AddComponent<ProtagonistManager>();
            
            var prefab = new GameObject("TestProtagonist");
            _manager.GetType().GetField("_protagonistPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_manager, prefab);

            Vector3 targetPos = Vector3.zero;
            _manager.EnsureProtagonist(targetPos);
            Transform firstTransform = _manager.ProtagonistTransform;

            // Act
            _manager.EnsureProtagonist(targetPos);

            // Assert
            Assert.AreEqual(firstTransform, _manager.ProtagonistTransform);
            
            // Cleanup
            Object.Destroy(prefab);
        }

        [UnityTest]
        public IEnumerator WalkInProtagonist_MovesToTargetPosition()
        {
            // Arrange
            var gameObject = new GameObject("TestManager");
            _manager = gameObject.AddComponent<ProtagonistManager>();
            
            var prefab = new GameObject("TestProtagonist");
            _manager.GetType().GetField("_protagonistPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_manager, prefab);
            _manager.GetType().GetField("_walkInDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_manager, 0.1f); // Fast for testing

            Vector3 targetPos = new Vector3(0, 5, 0);
            _manager.EnsureProtagonist(targetPos);

            // Act
            _manager.WalkInProtagonist(targetPos);

            // Wait for walk to complete (with buffer)
            yield return new WaitForSeconds(0.15f);

            // Assert
            Assert.AreEqual(targetPos, _manager.ProtagonistTransform.position);
            
            // Cleanup
            Object.Destroy(prefab);
        }
    }
}
