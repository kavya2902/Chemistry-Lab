using UnityEngine;

/// Attach to a beaker's liquid trigger child GameObject.
/// Stores what chemical solution is inside the beaker so
/// LitmusPaperController can determine the correct reaction.
///
/// SETUP (repeat for each beaker):
///   1. Inside the beaker, add a child GameObject (e.g. "LiquidTrigger").
///   2. Add a BoxCollider → set Is Trigger = true, resize to fill the liquid volume.
///   3. Set the GameObject's Tag to "Beaker1", "Beaker2", or "Beaker3".
///   4. Attach BeakerSolutionType → choose solutionType (Acid / Base / Neutral).
public class BeakerSolutionType : MonoBehaviour
{
    public enum SolutionType { Acid, Base, Neutral }

    [Tooltip("The chemical nature of the solution inside this beaker.")]
    public SolutionType solutionType = SolutionType.Neutral;
}
