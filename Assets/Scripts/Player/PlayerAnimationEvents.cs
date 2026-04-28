using System;
using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    public event Action HitFrameReached;
    public event Action CounterWindowClosed;
    public event Action ActionFinished;

    public void AnimationHit()
    {
        HitFrameReached?.Invoke();
    }

    public void AnimationCounterWindowClosed()
    {
        CounterWindowClosed?.Invoke();
    }

    public void AnimationFinish()
    {
        ActionFinished?.Invoke();
    }
}
