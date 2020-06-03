using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Randomizer {
    public partial class RandoLogic {
        private abstract class RandoTask {
            protected RandoLogic Logic;
            private int FrontCount = 0;
            private int BackCount = 0;
            private List<Receipt> Receipts = new List<Receipt>();

            public RandoTask(RandoLogic logic) {
                this.Logic = logic;
            }

            public abstract bool Next();

            protected void AddNextTask(RandoTask toPush) {
                this.Logic.Tasks.AddToFront(toPush);
                this.FrontCount++;
            }

            protected void AddLastTask(RandoTask toPush) {
                this.Logic.Tasks.AddToBack(toPush);
                this.BackCount++;
            }

            protected void AddReceipt(Receipt r) {
                this.Receipts.Add(r);
            }

            public void Undo() {
                while (this.FrontCount > 0) {
                    this.Logic.Tasks.RemoveFromFront();
                    this.FrontCount--;
                }

                while (this.BackCount > 0) {
                    this.Logic.Tasks.RemoveFromBack();
                    this.BackCount--;
                }

                this.Receipts.Reverse();
                foreach (var r in this.Receipts) {
                    r.Undo();
                }
                this.Receipts.Clear();
            }
        }
    }
}
