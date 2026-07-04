using System;
using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    PlayerController controller;

    void Awake() => controller = GetComponent<PlayerController>();

    //PlayerAnimationEvents - Controller - 实现
    public void AnimationHit()
    {
        controller?.AnimHitFrame();
    }

    public void AnimationCounterWindowClosed()
    {
        controller?.AnimCounterClosed();
    }

    public void AnimationFinish()
    {
        controller?.AnimFinished();
    }

    public void AnimationFootstep()
    {
        AudioManager.Instance?.PlayFootstep();
    }
}
