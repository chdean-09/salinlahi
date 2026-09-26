using NUnit.Framework;
using System;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyWalkFrameContractTests
    {
        [Test]
        public void EnemyExposesWalkFrameIndexAndPublishesChangesForOverlaySynchronization()
        {
            Type enemyType = typeof(Enemy);

            Assert.IsNotNull(enemyType.GetProperty("CurrentWalkFrameIndex"),
                "Overlay consumers need the frame currently shown by Enemy's existing animation clock.");
            Assert.IsNotNull(enemyType.GetProperty("WalkFrameCount"),
                "Frame-range definitions need the current loop length to validate their bounds.");
            Assert.IsNotNull(enemyType.GetEvent("WalkFrameChanged"),
                "Overlays need notification when the manual walk loop advances or resets.");
        }
    }
}
