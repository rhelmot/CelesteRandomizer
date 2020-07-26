Celeste Randomizer
==================

This is an [Everest](https://everestapi.github.io/) mod for [Celeste](http://www.celestegame.com/) that automatically generates randomly constructed maps by treating each room as a building block that can fit against certain other rooms in designated ways.

![settings menu](docs/img/settings.png)
![gameplay screenshot](docs/img/gameplay.png)
![debug mode map](docs/img/debug.png)

Installing
----------

I'll put this on gamebanana when I'm confident that it's ready. In the meantime, you can build it from source or download the beta [here](http://rhelmot.io/Randomizer_0.0.1.zip).

Player Advice
-------------

### Difficulty modes

The difficulty modes are intended as follows:

- *Normal:* Player is comfortable with the A-sides and can figure out a B-side room given some time
- *Hard:* Player is comfortable with Farewell and the C-Sides, knows a little bit of speedrun tech
- *Expert:* Player is comfortable with spike jumps, corner boosts, easy demodashes
- *Perfect:* Player knows the above tech and is using slowdown and savestates

All of this is entirely subjective and was categorized by me, so if you have any questions or suggestions on what should be easier or harder, let me know :)

### Map construction algorithms

- The *pathway* algorithm constructs a straight-line path from a start to an end. It may add side-routes to place keys necessary to proceed. It may not be traversable in reverse.
- The *labyrinth* algorithm constructs a sprawling map with no defined goals. It is designed for exploring and having fun. Every room is accessable from every other room; every passage can be traveresed in both directions with respect to the current number of dashes and player skill level.
