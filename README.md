# Spaced Repetition

I read a typically [insightful and extensively-research article from gwern](http://www.gwern.net/Spaced%20repetition) extolling the benefits of revisiting topics to reinforce your learning. I tried to do this manually, by keeping a little list of what techs I've used on and when. Of course human frailty got in the way and I forgot to keep it up. Given that most (non-work) things I do of any substance end up on GitHub, it seems a straightforward solution is to use GitHub's data on my activity and work out what I need from that.

## Targets (priority order)
1. I need to be able to find out (easily) when I last used a particular language or library.
2. I'd like to be able to find out how much effort I've put into each (number of commits is probably best proxy for this, rather than linecount).
3. Automated reminders? _"Time you exercised those Haskell muscles, Chris"_
3. Pretty visualizations?

## Plan
1. Explore a bit using F#.
2. Figure out rest of plan.
3. Looks like when I last updated a particular repo plus the main language of that repo is a terrible proxy for when I last used a language, since I (occasionally) diligently update READMEs for my projects or commit a minor fix. Going to focus on the following metric instead: commit rate.
4. Settled on desired output: for each language, a commit rate history (number of commits every day since I joined GitHub):
    Map<Language,CommitHistory> where CommitHistory = Map<Date,Commits>

From that it should be easy to calculate the following metrics:
* Last non-trivial activity per language
* Total activity per language
* Number of times doing significant work (i.e. number of peaks of activity) per language
