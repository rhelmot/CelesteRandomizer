Celeste Randomizer
==================

This is an [Everest](https://everestapi.github.io/) mod for [Celeste](http://www.celestegame.com/) that automatically generates randomly constructed maps by treating each room as a building block that can fit against certain other rooms in designated ways.

![settings menu](docs/img/settings.png)
![gameplay screenshot](docs/img/gameplay.png)
![debug mode map](docs/img/debug.png)


Installation
------------

Download from the [gamebanana page](https://gamebanana.com/tools/6848) and copy the zip file into your Celeste installation's `Mods` folder.


Player Advice
-------------

### Difficulty modes

The difficulty modes are intended as follows:

- *Normal:* Player is comfortable with the A-sides and can figure out a B-side room given some time
- *Hard:* Player is comfortable with Farewell and the C-Sides, knows a little bit of speedrun tech
- *Expert:* Player is comfortable with spike jumps, corner boosts, easy demodashes
- *Perfect:* Player knows the above tech and is using slowdown and savestates

If you need a difficulty easier than Normal, you should disable the maps that you don't feel comfortable with.

Of course, all of this is entirely subjective and was categorized by me, so if you have any questions or suggestions on what should be easier or harder, let me know :)

### Map construction algorithms

- The *Pathway* algorithm constructs a straight-line path from a start to an end. It may add side-routes to place keys necessary to proceed. It may not be traversable in reverse.
- The *Labyrinth* algorithm constructs a sprawling map with no defined goals. It is designed for exploring and having fun. Every room is accessable from every other room; every passage can be traversed in both directions with respect to the current number of dashes and player skill level.

### Known bugs

- https://github.com/rhelmot/CelesteRandomizer/issues/7

  Sometimes the randomizer will attach a room where the exit is a launcher boost out the top to an entrance with no ground to land on. Instead of slamming down onto the ground, Madeline will fall through to the previous room. The workaround is to pause and retry the room. If the retry option is greyed out, you need to wait longer, until Madeline reaches the ground of the previous room... pretty sketchy!

  On a similar note, in Labyrinth maps you may encounter a situation where you are launched into the air but the next room simply never loads. This means that the room is a dead end, and you should press Retry to backtrack.


Randomizing custom maps
-----------------------

You can add custom maps to the randomizer! All it takes is a lot of clerical work. For each room, you need to describe the exact ways the player is capable of moving from exit to exit in [this format](docs/metadata.md), and then bundle the configuration file with your map (or with another mod, we're not picky).
