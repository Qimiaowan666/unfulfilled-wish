using System;
using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    // Keep events so other systems (UI, VFX) can still subscribe
    public event Action HitFrameReached;
    public event Action CounterWindowClosed;
    public event Action ActionFinished;

    PlayerController controller;

    void Awake() => controller = GetComponent<PlayerController>();

    public void AnimationHit()
    {
        HitFrameReached?.Invoke();
        controller?.AnimHitFrame();
    }

    public void AnimationCounterWindowClosed()
    {
        CounterWindowClosed?.Invoke();
        controller?.AnimCounterClosed();
    }

    public void AnimationFinish()
    {
        ActionFinished?.Invoke();
        controller?.AnimFinished();
    }

    public void AnimationFootstep()
    {
        AudioManager.Instance?.PlayFootstep();
    }
}
