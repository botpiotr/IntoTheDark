using IntoTheDark.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace IntoTheDark.Tests
{
    public class LightZoneTests
    {
        private GameObject zoneObject;

        [TearDown]
        public void TearDown()
        {
            if (zoneObject != null)
                Object.DestroyImmediate(zoneObject);
        }

        private LightZone CreateZone(Vector3 center, float radius)
        {
            zoneObject = new GameObject("TestZone");
            zoneObject.transform.position = center;
            var zone = zoneObject.AddComponent<LightZone>();
            zone.Radius = radius;
            return zone;
        }

        [Test]
        public void PointInsideRadius_IsLit()
        {
            CreateZone(new Vector3(100f, 0f, 100f), 5f);
            Assert.IsTrue(LightZone.IsLit(new Vector3(103f, 0f, 100f)));
        }

        [Test]
        public void PointOutsideRadius_IsDark()
        {
            CreateZone(new Vector3(100f, 0f, 100f), 5f);
            Assert.IsFalse(LightZone.IsLit(new Vector3(106f, 0f, 100f)));
        }

        [Test]
        public void DisabledZone_DoesNotLight()
        {
            var zone = CreateZone(new Vector3(100f, 0f, 100f), 5f);
            zone.enabled = false;
            Assert.IsFalse(LightZone.IsLit(new Vector3(100f, 0f, 100f)));
        }
    }
}
