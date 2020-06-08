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

As of now, the metadata that allows the number-of-dashes and difficulty settings to work is only implemented for chapters 1A/1B/1C/2A/2B/2C/3A/3B/3C. All other maps will be randomized with their reachability checks assuming the hardest difficulty and two dashes settings regardless of what you select. If you want to help with this, check out the [metadata guide](docs/metadata.md) and consider contributing :)

It is highly recommended for you to disable the core levels pending [this issue](https://github.com/rhelmot/CelesteRandomizer/issues/3). They can and will lead to uncompletable maps.
