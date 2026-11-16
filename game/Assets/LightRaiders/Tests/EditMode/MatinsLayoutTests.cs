using System.Collections.Generic;
using System.Linq;
using LightRaiders.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LightRaiders.Tests
{
    /// <summary>
    /// Smoke test for the graybox Matins map (#36). Builds the map in memory with
    /// MatinsGenerator.BuildMap (no scene, no asset writes) and checks the shape
    /// the PRD asks for: counts, Small/Large tags, one Undercroft entrance per
    /// district, every door linked across a Layer boundary, LayerInfo coverage,
    /// and ADR-0008's physical separation between Layers.
    /// </summary>
    public sealed class MatinsLayoutTests
    {
        private MatinsGenerator.Layout _layout;

        [SetUp]
        public void SetUp() => _layout = MatinsGenerator.BuildMap(null);

        [TearDown]
        public void TearDown()
        {
            if (_layout?.Root != null)
                Object.DestroyImmediate(_layout.Root);
        }

        private LayerInfo[] Layers => _layout.Root.GetComponentsInChildren<LayerInfo>(true);
        private Door[] Doors => _layout.Root.GetComponentsInChildren<Door>(true);

        [Test]
        public void Map_HasThreeDistricts_EightTaggedInteriors_AndOneUndercroft()
        {
            District[] districts = _layout.Root.GetComponentsInChildren<District>(true);
            Assert.That(districts.Length, Is.EqualTo(MatinsGenerator.DistrictCount), "District count.");
            Assert.That(districts.Select(d => d.Index), Is.EquivalentTo(Enumerable.Range(0, MatinsGenerator.DistrictCount)), "District indices.");

            LayerInfo[] layers = Layers;
            Assert.That(layers.Select(l => l.LayerId).Distinct().Count(), Is.EqualTo(layers.Length), "Layer ids must be unique.");

            LayerInfo surface = layers.Single(l => l.LayerId == LayerMember.Surface);
            Assert.That(surface.Size, Is.EqualTo(LayerSize.Open));
            LayerInfo undercroft = layers.Single(l => l.LayerId == MatinsGenerator.UndercroftLayer);
            Assert.That(undercroft.Size, Is.EqualTo(LayerSize.Open));

            LayerInfo[] interiors = layers.Where(l => l.Size != LayerSize.Open).ToArray();
            Assert.That(interiors.Length, Is.EqualTo(8), "Phase 2 ships about 8 interiors (PRD #31 Q5).");
            Assert.That(interiors.Any(l => l.Size == LayerSize.Small), "No Small interior.");
            Assert.That(interiors.Any(l => l.Size == LayerSize.Large), "No Large interior.");
            Assert.That(layers.Length, Is.EqualTo(interiors.Length + 2), "Only Surface, Undercroft and interiors expected.");
        }

        [Test]
        public void EveryDoor_IsLinkedBothWays_AcrossALayerBoundary_AndEveryLayerIdHasInfo()
        {
            HashSet<int> infoIds = new HashSet<int>(Layers.Select(l => l.LayerId));
            Door[] doors = Doors;
            Assert.That(doors.Length, Is.GreaterThan(0));

            foreach (Door door in doors)
            {
                Assert.IsNotNull(door.Linked, door.name + " has no linked door.");
                Assert.AreSame(door, door.Linked.Linked, door.name + " is not linked back.");
                Assert.That(door.Linked.LayerId, Is.Not.EqualTo(door.LayerId), door.name + " links into its own Layer.");
                Assert.That(infoIds, Does.Contain(door.LayerId), "Layer " + door.LayerId + " has no LayerInfo.");

                // The door must sit inside the geometry of the Layer it claims.
                LayerInfo owner = door.GetComponentInParent<LayerInfo>();
                Assert.IsNotNull(owner, door.name + " is not under a Layer root.");
                Assert.That(owner.LayerId, Is.EqualTo(door.LayerId), door.name + " claims a Layer it is not built in.");
            }

            foreach (int id in infoIds.Where(id => id != LayerMember.Surface))
                Assert.That(doors.Any(d => d.LayerId == id), "Layer " + id + " is unreachable: it has no door.");
        }

        [Test]
        public void Undercroft_HasExactlyOneEntrancePerDistrict()
        {
            Door[] undercroftDoors = Doors.Where(d => d.LayerId == MatinsGenerator.UndercroftLayer).ToArray();
            Assert.That(undercroftDoors.Length, Is.EqualTo(MatinsGenerator.DistrictCount));

            int[] districtsServed = undercroftDoors
                .Select(d => d.Linked.GetComponentInParent<District>())
                .Select(district => { Assert.IsNotNull(district, "An Undercroft entrance is not in a district."); return district.Index; })
                .ToArray();
            Assert.That(districtsServed, Is.EquivalentTo(Enumerable.Range(0, MatinsGenerator.DistrictCount)),
                "Every district needs its own Undercroft entrance.");
            Assert.That(undercroftDoors.All(d => d.Linked.LayerId == LayerMember.Surface), "Undercroft entrances must open onto the Surface.");
        }

        [Test]
        public void ArrivingThroughADoor_NeverLandsInReachOfAnyDoor()
        {
            /* Anti-bounce: a held interact would otherwise re-trigger a door on
             * arrival. ExitPosition is where the LINKED door sends a Raider. */
            float minimum = Door.UseRadius + 0.25f;
            foreach (Door door in Doors)
            {
                foreach (Door other in Doors)
                {
                    Vector3 offset = other.transform.position - door.ExitPosition;
                    if (Mathf.Abs(offset.y) > Door.UseHeightTolerance)
                        continue;
                    offset.y = 0f;
                    Assert.That(offset.magnitude, Is.GreaterThan(minimum),
                        door.name + " (Layer " + door.LayerId + ") exits within reach of " + other.name + " (Layer " + other.LayerId + ").");
                }
            }
        }

        [Test]
        public void WaygatePair_StandsOnTheSurface_InTwoDifferentDistricts_ClearOfDoors()
        {
            Transform[] posts = _layout.WaygatePosts;
            Assert.That(posts.Length, Is.EqualTo(2));
            Assert.That(posts, Has.All.Not.Null);
            District a = posts[0].GetComponentInParent<District>();
            District b = posts[1].GetComponentInParent<District>();
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.That(a.Index, Is.Not.EqualTo(b.Index), "Both gates in one district.");

            foreach (Transform post in posts)
            {
                Assert.That(post.GetComponentInParent<LayerInfo>().LayerId, Is.EqualTo(LayerMember.Surface));
                Vector3 arrival = post.position + post.forward * Waygate.ExitDistance;
                foreach (Door door in Doors)
                {
                    Vector3 toPost = door.transform.position - post.position;
                    Vector3 toArrival = door.transform.position - arrival;
                    toPost.y = 0f;
                    toArrival.y = 0f;
                    Assert.That(toPost.magnitude, Is.GreaterThan(Door.UseRadius + Waygate.UseRadius), post.name + " overlaps " + door.name + "'s reach.");
                    Assert.That(toArrival.magnitude, Is.GreaterThan(Door.UseRadius + 0.25f), post.name + "'s arrival is in " + door.name + "'s reach.");
                }
            }
        }

        [Test]
        public void TwoExfilSites_OnTheSurface_InDifferentDistricts_ZonesClearOfSpawns()
        {
            Transform[] posts = _layout.ExfilPosts;
            Assert.That(posts.Length, Is.EqualTo(2), "PRD #31: two Exfil sites.");
            Assert.That(posts[0].GetComponentInParent<District>().Index, Is.Not.EqualTo(posts[1].GetComponentInParent<District>().Index));
            foreach (Transform post in posts)
            {
                Assert.That(post.GetComponentInParent<LayerInfo>().LayerId, Is.EqualTo(LayerMember.Surface));
                foreach (Transform spawn in _layout.SpawnPoints)
                {
                    Vector3 offset = spawn.position - post.position;
                    offset.y = 0f;
                    Assert.That(offset.magnitude, Is.GreaterThan(ExfilSite.ZoneRadius), "A spawn point sits inside an Exfil zone.");
                }
            }
        }

        [Test]
        public void SpawnPoints_AreOnTheSurface_TwoPerDistrict()
        {
            Assert.That(_layout.SpawnPoints.Length, Is.EqualTo(MatinsGenerator.DistrictCount * 2));
            foreach (Transform spawn in _layout.SpawnPoints)
            {
                Assert.IsNotNull(spawn.GetComponentInParent<District>(), spawn.name + " is not in a district.");
                Assert.That(spawn.GetComponentInParent<LayerInfo>().LayerId, Is.EqualTo(LayerMember.Surface));
            }
        }

        [Test]
        public void LayerGeometry_NeverOverlaps_ByADR0008Margin()
        {
            /* ADR-0008: Layers share one physics scene, so separation is purely
             * spatial. Keep every Layer's footprint far apart, well beyond a shot's
             * reach, so nothing can be seen or hit across a boundary. */
            const float margin = 100f;
            LayerInfo[] layers = Layers;
            Bounds[] bounds = layers.Select(OwnGeometryBounds).ToArray();

            for (int i = 0; i < layers.Length; i++)
            {
                for (int j = i + 1; j < layers.Length; j++)
                {
                    Bounds grown = bounds[i];
                    grown.Expand(margin * 2f);
                    Assert.That(grown.Intersects(bounds[j]), Is.False,
                        layers[i].DisplayName + " and " + layers[j].DisplayName + " are closer than " + margin + " m.");
                }
            }
        }

        /// <summary>Renderer bounds of a Layer root's own geometry (nested Layer roots excluded).</summary>
        private static Bounds OwnGeometryBounds(LayerInfo layer)
        {
            Renderer[] renderers = layer.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.GetComponentInParent<LayerInfo>() == layer)
                .ToArray();
            Assert.That(renderers.Length, Is.GreaterThan(0), layer.DisplayName + " has no geometry.");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
