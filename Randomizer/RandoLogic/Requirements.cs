using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    using FlagSet = Dictionary<string, FlagState>;

    public abstract class Requirement : IComparable<Requirement>, IComparable {
        public abstract bool Able(Capabilities state);

        public virtual Requirement Conflicts(Capabilities state) {
            if (this.Able(state)) {
                return new Possible();
            }
            return this;
        }

        public virtual int Complexity() {
            return 1;
        }

        public int CompareTo(Requirement obj) {
            return this.Complexity().CompareTo(obj.Complexity());
        }

        public int CompareTo(object obj) {
            if (!(obj is Requirement other)) {
                throw new ArgumentException("Can only compare Requirement to Requirement");
            }
            return this.CompareTo(other);
        }

        public abstract bool Equals(Requirement other);

        protected static void Normalize(List<Requirement> requirements) {
            // XXX EXTREMELY IMPORTANT XXX
            // MAKE SURE GetHashCode NEVER CALLS ANY BASE CLASS GetHashCode
            // OTHERWISE YOU WILL SEE BEHAVIOR DIVERGENCES BETWEEN C# VERSIONS
            requirements.Sort((Requirement a, Requirement b) => a.GetHashCode().CompareTo(b.GetHashCode()));
            for (int i = 0; i < requirements.Count - 1; ) {
                var a = requirements[i];
                var b = requirements[i + 1];
                if (a.Equals(b)) {
                    requirements.RemoveAt(i + 1);
                } else {
                    i++;
                }
            }
        }

        public static Requirement And(IEnumerable<Requirement> things) {
            var children = new List<Requirement>();
            foreach (var thing in things) {
                if (thing is Conjunction c) {
                    children.AddRange(c.Children);
                } else {
                    children.Add(thing);
                }
            }

            for (int i = 0; i < children.Count; ) {
                if (children[i] is Possible) {
                    children.RemoveAt(i);
                } else if (children[i] is Impossible) {
                    return children[i];
                } else {
                    i++;
                }
            }

            Normalize(children);
            if (children.Count == 0) {
                return new Possible();
            }
            if (children.Count == 1) {
                return children[0];
            }

            return new Conjunction(children);
        }

        public static Requirement Or(IEnumerable<Requirement> things) {
            var children = new List<Requirement>();
            foreach (var thing in things) {
                if (thing is Disjunction c) {
                    children.AddRange(c.Children);
                } else {
                    children.Add(thing);
                }
            }

            for (int i = 0; i < children.Count;) {
                if (children[i] is Impossible) {
                    children.RemoveAt(i);
                } else if (children[i] is Possible) {
                    return children[i];
                } else {
                    i++;
                }
            }

            Normalize(children);
            if (children.Count == 0) {
                return new Impossible();
            }
            if (children.Count == 1) {
                return children[0];
            }

            return new Disjunction(children);
        }

        public virtual bool StrictlyBetterThan(Requirement other) {
            if (other is Possible) {
                return false;
            }
            if (other is Impossible) {
                return true;
            }
            if (other is Conjunction c) {
                foreach (var child in c.Children) {
                    if (child.Equals(this)) {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }
    }

    public class Impossible : Requirement {
        public override bool Able(Capabilities state) {
            return false;
        }

        public override int Complexity() {
            return int.MaxValue;
        }

        public override bool Equals(Requirement other) {
            if (!(other is Impossible i)) {
                return false;
            }
            return true;
        }

        public override int GetHashCode() {
            return 1111;
        }

        public override bool StrictlyBetterThan(Requirement other) {
            return false;
        }
    }

    public class Possible : Requirement {
        public override bool Able(Capabilities state) {
            return true;
        }

        public override int Complexity() {
            return 0;
        }

        public override bool Equals(Requirement other) {
            if (!(other is Possible i)) {
                return false;
            }
            return true;
        }

        public override int GetHashCode() {
            return 2222;
        }

        public override bool StrictlyBetterThan(Requirement other) {
            return true;
        }
    }

    public class Conjunction : Requirement {
        public readonly List<Requirement> Children;
        public Conjunction(List<Requirement> Children) {
            Requirement.Normalize(Children);
            this.Children = Children;
        }

        public override bool Able(Capabilities state) {
            foreach (var child in this.Children) {
                if (!child.Able(state)) {
                    return false;
                }
            }
            return true;
        }

        public override Requirement Conflicts(Capabilities state) {
            var failures = new List<Requirement>();
            foreach (var child in this.Children) {
                var failure = child.Conflicts(state);
                if (failure is Possible) {
                    continue;
                }

                if (failure is Impossible) {
                    return failure;
                }
                failures.Add(failure);
            }

            if (failures.Count == 0) {
                return new Possible();
            } else if (failures.Count == 1) {
                return failures[0];
            } else {
                return new Conjunction(failures);
            }
        }

        public override int Complexity() {
            int result = 0;
            foreach (var child in Children) {
                result += child.Complexity();
            }
            return result;
        }

        public override bool Equals(Requirement other) {
            // TODO: this assumes that hashes are unique...

            if (!(other is Conjunction i)) {
                return false;
            }
            if (this.Children.Count != i.Children.Count) {
                return false;
            }
            for (int j = 0; j < this.Children.Count; j++) {
                if (!this.Children[j].Equals(i.Children[j])) {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode() {
            int result = 7777;
            foreach (var child in this.Children) {
                result ^= child.GetHashCode();
                result = (result << 10) | (result >> 22);
            }
            return result;
        }

        public override bool StrictlyBetterThan(Requirement other) {
            if (other is Possible) {
                return false;
            }
            if (other is Impossible) {
                return true;
            }
            if (other is Conjunction c) {
                // in order for this to be strictly better than other, all of this' elements must be present in other
                // and other must have extra elements
                if (this.Children.Count >= c.Children.Count) {
                    return false;
                }
                foreach (var thisChild in this.Children) {
                    var found = false;
                    foreach (var cChild in c.Children) {
                        if (cChild.Equals(thisChild)) {
                            found = true;
                            break;
                        }
                    }
                    if (!found) {
                        return false;
                    }
                }
                return true;
            }

            // a conjunction cannot possibly be a strict improvement over something which is not a conjunction
            return false;
        }
    }

    public class Disjunction : Requirement {
        public readonly List<Requirement> Children;
        public Disjunction(List<Requirement> Children) {
            Requirement.Normalize(Children);
            this.Children = Children;
        }

        public override bool Able(Capabilities state) {
            foreach (var child in this.Children) {
                if (child.Able(state)) {
                    return true;
                }
            }
            return false;
        }

        public override Requirement Conflicts(Capabilities state) {
            var failures = new List<Requirement>();
            foreach (var child in this.Children) {
                var failure = child.Conflicts(state);
                if (failure is Possible) {
                    return failure;
                }

                if (failure is Impossible) {
                    continue;
                }
                failures.Add(failure);
            }

            if (failures.Count == 0) {
                return new Impossible();
            } else if (failures.Count == 1) {
                return failures[0];
            } else {
                return new Disjunction(failures);
            }
        }

        public override int Complexity() {
            int result = int.MaxValue;
            foreach (var child in Children) {
                result = Math.Min(result, child.Complexity());
            }
            return result;
        }

        public override bool Equals(Requirement other) {
            // TODO: this assumes that hashes are unique...

            if (!(other is Disjunction i)) {
                return false;
            }
            if (this.Children.Count != i.Children.Count) {
                return false;
            }
            for (int j = 0; j < this.Children.Count; j++) {
                if (!this.Children[j].Equals(i.Children[j])) {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode() {
            int result = 8888;
            foreach (var child in this.Children) {
                result ^= child.GetHashCode();
                result = (result << 10) | (result >> 22);
            }
            return result;
        }

        public override bool StrictlyBetterThan(Requirement other) {
            if (base.StrictlyBetterThan(other)) {
                return true;
            }
            if (other is Disjunction d) {
                // for this to be a strict improvement, this must be larger than other and contain all other's elements
                if (this.Children.Count <= d.Children.Count) {
                    return false;
                }
                foreach (var dChild in d.Children) {
                    var found = false;
                    foreach (var thisChild in this.Children) {
                        if (thisChild.Equals(dChild)) {
                            found = true;
                            break;
                        }
                    }
                    if (!found) {
                        return false;
                    }
                }
                return true;
            }

            // can be considered an improvement if the child is a member of the disjunction
            if (this.Children.Count < 2) {
                return false;
            }
            foreach (var child in this.Children) {
                if (child.Equals(other)) {
                    return true;
                }
            }
            return false;
        }
    }

    public class DashRequirement : Requirement {
        public readonly NumDashes Dashes;
        public DashRequirement(NumDashes dashes) {
            this.Dashes = dashes;
        }

        public override bool Able(Capabilities state) {
            return state.Dashes >= this.Dashes;
        }

        public override Requirement Conflicts(Capabilities state) {
            if (this.Able(state)) {
                return new Possible();
            }

            if (state.Dashes == NumDashes.Zero || true) { // TODO actually think about this
                return new Impossible();
            }
            return this;
        }

        public override bool Equals(Requirement other) {
            if (!(other is DashRequirement i)) {
                return false;
            }
            return this.Dashes == i.Dashes;
        }

        public override int GetHashCode() {
            return 3333 ^ (int)this.Dashes;
        }

        public override bool StrictlyBetterThan(Requirement other) {
            if (base.StrictlyBetterThan(other)) {
                return true;
            }
            if (other is DashRequirement d) {
                return this.Dashes < d.Dashes;
            }
            return false;
        }
    }

    public class SkillRequirement : Requirement {
        public readonly Difficulty Difficulty;
        public SkillRequirement(Difficulty Difficulty) {
            this.Difficulty = Difficulty;
        }

        public override bool Able(Capabilities state) {
            return state.PlayerSkill >= this.Difficulty;
        }

        public override Requirement Conflicts(Capabilities state) {
            if (!this.Able(state)) {
                return new Impossible();
            }
            return new Possible();
        }

        public override bool Equals(Requirement other) {
            if (!(other is SkillRequirement i)) {
                return false;
            }
            return this.Difficulty == i.Difficulty;
        }

        public override int GetHashCode() {
            return 4444 ^ (int)this.Difficulty;
        }

        public override bool StrictlyBetterThan(Requirement other) {
            if (base.StrictlyBetterThan(other)) {
                return true;
            }
            if (other is SkillRequirement s) {
                return this.Difficulty < s.Difficulty;
            }
            return false;
        }
    }

    public class KeyRequirement : Requirement {
        public readonly int KeyholeID;
        public KeyRequirement(int keyholeID) {
            this.KeyholeID = keyholeID;
        }

        public override bool Able(Capabilities state) {
            return state.HasKey;
        }

        public override bool Equals(Requirement other) {
            if (!(other is KeyRequirement i)) {
                return false;
            }

            return this.KeyholeID == i.KeyholeID;
        }

        public override int GetHashCode() {
            return 5555;
        }
    }

    public class FlagRequirement : Requirement {
        public readonly string Flag;
        public readonly bool Set;
        public FlagRequirement(string Flag, bool Set) {
            this.Flag = Flag;
            this.Set = Set;
        }

        public override bool Able(Capabilities state) {
            if (state.BypassFlags) {
                return true;
            }

            var stateFlag = state.GetFlag(this.Flag);
            if (this.Set) {
                return stateFlag == FlagState.Set || stateFlag == FlagState.UnsetToSet || stateFlag == FlagState.Both;
            } else {
                return stateFlag == FlagState.Unset || stateFlag == FlagState.SetToUnset || stateFlag == FlagState.Both;
            }
        }

        public override bool Equals(Requirement other) {
            if (!(other is FlagRequirement i)) {
                return false;
            }
            return this.Flag == i.Flag && this.Set == i.Set;
        }

        public override int GetHashCode() {
            var result = 6666;
            foreach (var ch in this.Flag) {
                result ^= ch;
                result = (result << 10) | (result >> 22);
            }

            result ^= this.Set ? 1234 : 5678;
            return result;
        }
    }

    public class Capabilities {
        public NumDashes Dashes;
        public NumDashes RefillDashes;
        public Difficulty PlayerSkill;

        public bool HasKey;
        public bool BypassFlags;
        public FlagSet Flags;

        public FlagState GetFlag(string name) {
            return this.Flags.TryGetValue(name, out var result) ? result : FlagState.Unset;
        }

        public Capabilities Copy() {
            return new Capabilities {
                Dashes = this.Dashes,
                RefillDashes = this.RefillDashes,
                PlayerSkill = this.PlayerSkill,

                HasKey = this.HasKey,
                Flags = this.Flags,
            };
        }

        public Capabilities WithoutKey() {
            var result = this.Copy();
            result.HasKey = false;
            return result;
        }
        
        public Capabilities WithFlags(FlagSet flags) {
            var result = this.Copy();
            result.Flags = flags;
            return result;
        }

        public Capabilities WithoutFlags() {
            var result = this.Copy();
            result.BypassFlags = true;
            return result;
        }
    }

    public enum FlagState {
        Set, Unset, Both, One, SetToUnset, UnsetToSet,
    }
}
