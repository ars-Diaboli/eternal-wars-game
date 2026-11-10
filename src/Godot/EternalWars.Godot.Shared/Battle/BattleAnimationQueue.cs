using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// The battle view's one sequential animation queue: each step adds its tweeners to a tween of <paramref name="owner"/>, then sets its end
/// state. Godot cannot append to a tween once it has started playing, so steps arriving later (an AI turn after the last animation began)
/// wait for the next tween. <paramref name="busy"/> runs when the first step is queued, <paramref name="idle"/> each time the queue has
/// caught up (also after a step applied at once).
/// </summary>
public sealed class BattleAnimationQueue(Node owner, Action busy, Action idle)
{
    private readonly List<QueuedStep> _playing = [];
    private readonly List<QueuedStep> _waiting = [];
    private Tween? _queue;
    private ulong _queueFrame;
    private int _pending;
    private AnimationSpeed _speed = AnimationSpeed.Normal;

    /// <summary>Steps are still queued or playing.</summary>
    public bool IsBusy => _pending > 0;

    /// <summary>How fast the steps play (ADR-090); instant applies each step at once. A change applies to the tween playing.</summary>
    public AnimationSpeed Speed
    {
        get => _speed;
        set
        {
            _speed = value;
            if (_queue is not null && _queue.IsValid() && value != AnimationSpeed.Instant)
            {
                _queue.SetSpeedScale((float)SpeedSettings.Scale(value));
            }
        }
    }

    /// <summary>
    /// Queues one step: <paramref name="animate"/> adds its tweeners, then <paramref name="apply"/> sets the end state. With
    /// <paramref name="instant"/> (quick combat) or at instant speed everything queued and this step apply at once; with nothing queued, a
    /// step without animation applies at once.
    /// </summary>
    public void Play(Action<Tween>? animate, Action apply, bool instant)
    {
        if (instant || _speed == AnimationSpeed.Instant)
        {
            Flush();
            apply();
            idle();
            return;
        }

        if (animate is null && _pending == 0)
        {
            apply();
            idle();
            return;
        }

        if (++_pending == 1)
        {
            busy();
        }

        var step = new QueuedStep(animate, apply);
        if (_queue is not null && _queue.IsValid() && _queue.IsRunning())
        {
            if (_queueFrame == Engine.GetProcessFrames())
            {
                Append(_queue, step); // the tween has not played a frame yet
            }
            else
            {
                _waiting.Add(step);
            }

            return;
        }

        Start([step]);
    }

    /// <summary>Stops the animations and applies every queued step in order (quick combat mid-animation).</summary>
    public void Flush()
    {
        if (_pending == 0)
        {
            return;
        }

        _queue?.Kill();
        _queue = null;
        foreach (var step in _playing.Concat(_waiting).ToList())
        {
            step.Done = true;
            step.Apply();
        }

        Reset();
    }

    /// <summary>Stops the animations and drops every queued step.</summary>
    public void Reset()
    {
        _queue?.Kill();
        _queue = null;
        _playing.Clear();
        _waiting.Clear();
        _pending = 0;
    }

    private void Start(IEnumerable<QueuedStep> steps)
    {
        var queue = owner.CreateTween();
        queue.SetSpeedScale((float)SpeedSettings.Scale(_speed == AnimationSpeed.Instant ? AnimationSpeed.Normal : _speed));
        queue.Finished += () =>
        {
            if (_queue == queue && _waiting.Count > 0)
            {
                var next = _waiting.ToList();
                _waiting.Clear();
                Start(next);
            }
        };
        _queue = queue;
        _queueFrame = Engine.GetProcessFrames();
        foreach (var step in steps)
        {
            Append(queue, step);
        }
    }

    private void Append(Tween queue, QueuedStep step)
    {
        _playing.Add(step);
        step.Animate?.Invoke(queue);
        queue.TweenCallback(Callable.From(() => Finish(step)));
    }

    private void Finish(QueuedStep step)
    {
        if (step.Done)
        {
            return;
        }

        step.Done = true;
        _playing.Remove(step);
        step.Apply();
        _pending--;
        if (_pending == 0)
        {
            idle();
        }
    }

    /// <summary>One queued animation step and its end state.</summary>
    private sealed class QueuedStep(Action<Tween>? animate, Action apply)
    {
        public Action<Tween>? Animate { get; } = animate;

        public Action Apply { get; } = apply;

        public bool Done { get; set; }
    }
}
