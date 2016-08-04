﻿using System;
using System.Collections.Generic;
using System.Linq;
using Backend.ThreeAddressCode.Values;
using Backend.ThreeAddressCode.Instructions;
using Backend.Analysis;
using Backend.Visitors;
using Microsoft.Cci;

namespace ScopeAnalyzer
{
    /// <summary>
    /// Domain that keeps track of which variables may have escaped.
    /// </summary>
    public class VarEscapeSet : SetDomain<IVariable>
    {
        public VarEscapeSet(HashSet<IVariable> vesc)
        {
            elements = vesc;
        }

        public static VarEscapeSet Bottom
        {
            get { return new VarEscapeSet(new HashSet<IVariable>()); }
        }

        public static VarEscapeSet Top
        {
            get { return new VarEscapeSet(null); }
        }


        public void SetAllEscaped()
        {
            base.SetTop();
        }

        public void Escape(IVariable v)
        {
            base.Add(v);
        }

        public bool Escaped(IVariable v)
        {
            return base.Contains(v);
        }

        public VarEscapeSet Clone()
        {
            var nvesc = elements == null ? elements : new HashSet<IVariable>(elements);
            return new VarEscapeSet(nvesc);
        }

        public override string ToString()
        {
            string summary = String.Empty;
            if (IsTop) summary += "All variables may escape.";
            else
            {
                summary += "May escaped variables:\n";
                foreach (var v in elements)
                {
                    summary += String.Format("\t{0} ({1})\n", v.ToString(), v.Type);
                }
            }
            return summary;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Domain that keeps track of which fields may have escaped, fields being fixed.
    /// </summary>
    public class FieldEscapeSet
    {
        Dictionary<IFieldDefinition, Boolean> fieldsEscaped;

        private FieldEscapeSet(Dictionary<IFieldDefinition, Boolean> fe)
        {
            fieldsEscaped = fe;
        }

        public static FieldEscapeSet Bottom(IEnumerable<IFieldDefinition> fields)
        {
            Dictionary<IFieldDefinition, Boolean> fe = new Dictionary<IFieldDefinition, bool>();
            foreach (var f in fields)
            {
                fe[f] = false;
            }
            return new FieldEscapeSet(fe);
        }

        public static FieldEscapeSet Top(IEnumerable<IFieldDefinition> fields)
        {
            Dictionary<IFieldDefinition, Boolean> fe = new Dictionary<IFieldDefinition, bool>();
            foreach (var f in fields)
            {
                fe[f] = true;
            }
            return new FieldEscapeSet(fe);
        }

        public bool IsTop
        {
            get { return fieldsEscaped.Values.All(b => b); }
        }

        public bool IsBottom
        {
            get { return fieldsEscaped.Values.All(b => !b); }
        }

        public void Escape(IFieldDefinition f)
        {
            if (!fieldsEscaped.ContainsKey(f)) throw new InvalidFieldsDomainOperation("Field not in the domain!");
            fieldsEscaped[f] = true;
        }

        public bool Escaped(IFieldDefinition f)
        {
            if (!fieldsEscaped.ContainsKey(f)) throw new InvalidFieldsDomainOperation("Field not in the domain!");
            return fieldsEscaped[f];
        }

        public void SetAllEscaped()
        {
            for (int i = 0; i < fieldsEscaped.Count; i++)
            {
                fieldsEscaped[fieldsEscaped.Keys.ElementAt(i)] = true;
            }
        }

        public int Count
        {
            get { return fieldsEscaped.Count; }
        }

        public override bool Equals(object obj)
        {
            var other = obj as FieldEscapeSet;
            if (this.Count != other.Count) return false;

            foreach (var f in fieldsEscaped.Keys)
            {
                if (this.Escaped(f) != other.Escaped(f)) return false;
            }

            return true;
        }

        public void Join(FieldEscapeSet fs)
        {
            if (this.Count != fs.Count) throw new InvalidFieldsDomainOperation("Field not in the domain!");
            for (int i = 0; i < fieldsEscaped.Keys.Count; i++)
            {
                var f = fieldsEscaped.Keys.ElementAt(i);
                fieldsEscaped[f] |= fs.Escaped(f);
            }
        }

        public FieldEscapeSet Clone()
        {
            return new FieldEscapeSet(new Dictionary<IFieldDefinition, bool>(fieldsEscaped));
        }

        public override string ToString()
        {
            string summary = "May escape information about fields:\n";
            foreach (var f in fieldsEscaped.Keys)
            {
                summary += String.Format("\t{0}: {1}\t{2}\n", f.ContainingType.FullName() + "::" + f.Name, fieldsEscaped[f], f.Type);
            }
            return summary;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        class InvalidFieldsDomainOperation : Exception
        {
            public InvalidFieldsDomainOperation(string message) : base(message) { }
        }
    }

    /// <summary>
    /// Lattice product between VarEscapeSet and FieldEscapeSet.
    /// </summary>
    public class ScopeEscapeDomain
    {
        VarEscapeSet vset;
        FieldEscapeSet fset;

        private ScopeEscapeDomain(VarEscapeSet vs, FieldEscapeSet fs)
        {
            vset = vs;
            fset = fs;
        }


        public static ScopeEscapeDomain Top(IEnumerable<IFieldDefinition> fdefs)
        {
            return new ScopeEscapeDomain(VarEscapeSet.Top, FieldEscapeSet.Top(fdefs));
        }

        public static ScopeEscapeDomain Bottom(IEnumerable<IFieldDefinition> fdefs)
        {
            return new ScopeEscapeDomain(VarEscapeSet.Bottom, FieldEscapeSet.Bottom(fdefs));
        }

        public bool IsTop
        {
            get { return vset.IsTop && fset.IsTop; }
        }

        public bool IsBottom
        {
            get { return vset.IsBottom && fset.IsBottom; }
        }

        public VarEscapeSet Variables
        {
            get { return vset; }
        }

        public FieldEscapeSet Fields
        {
            get { return fset; }
        }

        public void Escape(IVariable v)
        {
            vset.Escape(v);
        }

        public void Escape(IFieldDefinition f)
        {
            fset.Escape(f);
        }

        public bool Escaped(IVariable v)
        {
            return vset.Escaped(v);
        }

        public bool Escaped(IFieldDefinition f)
        {
            return fset.Escaped(f);
        }

        public void SetAllEscaped()
        {
            vset.SetAllEscaped();
            fset.SetAllEscaped();
        }

        public void Join(ScopeEscapeDomain sed)
        {
            var nvset = vset.Clone();        
            nvset.Join(sed.Variables);
            vset = nvset;

            var nfset = fset.Clone();
            nfset.Join(sed.Fields);
            fset = nfset;
        }


        public override bool Equals(object obj)
        {
            var other = obj as ScopeEscapeDomain;
            return other.Variables.Equals(vset) && other.Fields.Equals(fset);
        }

        public ScopeEscapeDomain Clone()
        {
            var v = vset.Clone();
            var f = fset.Clone();
            return new ScopeEscapeDomain(v, f);
        }

        public override string ToString()
        {
            return fset.ToString() + "\n" + vset.ToString();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }


    /*
     * This is a very naive implementation of an escape analysis particularly targeted for Scope Reducer/Processor
     * methods. We implicitly assume that all reference values may be escaped so we don't need to keep track of them. 
     * The only exception are the variables of type Row/Rowset. We know that special "this" Row(set) fields do not 
     * escape upon entering the method. That being said, the analysis assumes that all other fields and array elements
     * may escape and overapproximates the escapage of Row(set) variables and the mentioned fields. 
     * Currently, we assume all Row(set) variables may alias. TODO: include aliasing information.
     */
    public class NaiveScopeMayEscapeAnalysis : ForwardDataFlowAnalysis<ScopeEscapeDomain>
    {
        IMethodDefinition method;
        ITypeDefinition rowType;
        ITypeDefinition rowsetType;
        Dictionary<Instruction, ScopeEscapeDomain> results = new Dictionary<Instruction, ScopeEscapeDomain>();
        List<IFieldDefinition> fieldsToTrack = new List<IFieldDefinition>();
        IMetadataHost host;
        bool hasExceptions = false;

        public NaiveScopeMayEscapeAnalysis(ControlFlowGraph cfg, IMethodDefinition m, IMetadataHost h, ITypeDefinition rowtype, ITypeDefinition rowsettype) : base(cfg)
        {
            method = m;
            host = h;

            rowType = rowtype;
            rowsetType = rowsettype;

            Initialize();
        }

    

        #region Dataflow interface implementation

        protected override ScopeEscapeDomain InitialValue(CFGNode node)
        {
            if (hasExceptions)
            {
                return ScopeEscapeDomain.Top(fieldsToTrack);
            }
            else
            {
                return ScopeEscapeDomain.Bottom(fieldsToTrack);
            }
        }

        protected override ScopeEscapeDomain Join(ScopeEscapeDomain left, ScopeEscapeDomain right)
        {
            var join = left.Clone();
            join.Join(right);
            return join;
        }

        protected override ScopeEscapeDomain Flow(CFGNode node, ScopeEscapeDomain input)
        {
            var nState = input.Clone();
            var visitor = new EscapeTransferVisitor(nState, this);
            visitor.Visit(node);
            UpdateResults(visitor.PostStates);
            return visitor.State.Clone();
        }

        protected override bool Compare(ScopeEscapeDomain left, ScopeEscapeDomain right)
        {
            return left.Equals(right);
        }

        #endregion


        public IMetadataHost Host
        {
            get { return host; }
        }

        public bool HasExceptions
        {
            get { return hasExceptions; }
        }

        public Dictionary<Instruction, ScopeEscapeDomain> Results
        {
            get { return results; }
        }



        private void Initialize()
        {
            var mtype = (method.ContainingType as INamedTypeReference).Resolve(host);
            // Now we find fields to track.
            foreach (var field in mtype.Fields)
            {
                // Skip this, as it references an escaped "environement".
                if (field.Name.Value.EndsWith("__this")) continue;

                if (!field.IsStatic && PossiblyRow(field.Type)) fieldsToTrack.Add(field);
            }

            var instructions = new List<Instruction>();
            foreach(var block in cfg.Nodes)
                instructions.AddRange(block.Instructions);

            if (instructions.Any(i => i is ThrowInstruction || i is CatchInstruction))
                hasExceptions = true;
        }

        private void UpdateResults(Dictionary<Instruction, ScopeEscapeDomain> states)
        {
            foreach (var key in states.Keys)
            {
                results[key] = states[key];
            }
        }

        public ITypeDefinition RowType
        {
            get { return rowType; }
        }

        public IEnumerable<IFieldDefinition> TrackedFields
        {
            get { return fieldsToTrack; }
        }

        public bool PossiblyRow(ITypeReference type)
        {
            return PossiblyRow(type, rowType, rowsetType, host);
        }

        public static bool PossiblyRow(ITypeReference type, ITypeDefinition rowType, ITypeDefinition rowsetType, IMetadataHost host)
        {
            if (!(type is INamedTypeReference)) return false;

            var ntr = type as INamedTypeReference;
            if (ntr.IsValueType || ntr.IsEnum) return false;

            var resolved = ntr.Resolve(host);
            //return true for soundness!
            if (resolved == null || resolved.IsDummy()) return true;
            if (!resolved.IsReferenceType) return false;

            if (resolved.SubtypeOf(rowType)) return true;
            if (resolved.SubtypeOf(rowsetType)) return true;

            return false;
        }


        /* 
        * All reference variables except Row/Rowset are assumed to be escaped. A Row(set) variable is set as escaped if (1) 
        * it is assigned to a static variable, (2) it is assigned to a field, (3) it is passed as a parameter to a foreign 
        * method or it is a result of such a method, or (4) it is being assigned by an escaped Row(set) variable/field. 
        * Hence, the computed escape variables should all be of type Row. However, sometimes the actual types are not 
        * available so we safely add such variables as escaped, just to be sure.
        */
        class EscapeTransferVisitor : InstructionVisitor
        {
            ScopeEscapeDomain currentState;
            NaiveScopeMayEscapeAnalysis parent;

            Dictionary<Instruction, ScopeEscapeDomain> preState = new Dictionary<Instruction, ScopeEscapeDomain>();
            Dictionary<Instruction, ScopeEscapeDomain> postState = new Dictionary<Instruction, ScopeEscapeDomain>();

            public EscapeTransferVisitor(ScopeEscapeDomain start, NaiveScopeMayEscapeAnalysis dad)
            {
                SetCurrent(start);
                parent = dad;
            }



            #region Transfer functions

            public override void Visit(IInstructionContainer container)
            {
                preState.Clear();
                postState.Clear();
                base.Visit(container);
            }

            public override void Visit(Instruction instruction)
            {
                Default(instruction);
            }

            public override void Visit(DefinitionInstruction instruction)
            {
                Default(instruction);
            }

            public override void Default(Instruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                SetCurrent(FreshCurrent());
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(StoreInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());

                var nstate = FreshCurrent();
                var result = instruction.Result;
                var operand = instruction.Operand;

                if (PossiblyRow(operand.Type))
                {
                    if (result is InstanceFieldAccess)
                    {             
                        var r = result as InstanceFieldAccess;

                        var fdef = MatchFieldDefinition(r.Field);
                        if (fdef != null)
                        {
                            if (nstate.Escaped(operand))
                                UpdateState(nstate, fdef, instruction);
                        }
                        else
                        {
                            UpdateState(nstate, operand, instruction);
                        }
                    }
                    else
                    {
                        UpdateState(nstate, operand, instruction);
                    }
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(LoadInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();
                var result = instruction.Result;
                var operand = instruction.Operand;

                // If the operand has anything to do with pointers, then we set result as escaped.
                if (operand is Dereference || operand is Reference)
                {
                    //TODO: make more precise
                    UpdateStateEscaped(nstate);
                }
                else if (PossiblyRow(result.Type))
                {    
                    if (operand is InstanceFieldAccess)
                    {
                        var op = operand as InstanceFieldAccess;
                        var fdef = MatchFieldDefinition(op.Field);

                        if (fdef == null)
                        {
                            UpdateState(nstate, result, instruction);
                        } else
                        {
                            if (nstate.Escaped(fdef))
                                UpdateState(nstate, result, instruction);
                            else if (nstate.Escaped(result))
                                UpdateState(nstate, fdef, instruction);
                        }
                    }
                    // Set resuls as escaped in this case
                    else if (operand is UnknownValue || operand is StaticFieldAccess || operand is ArrayElementAccess)
                    {
                        UpdateState(nstate, result, instruction);
                    }
                    // TODO: should this case even occur? Typing should not allow this.
                    else if (operand is VirtualMethodReference || operand is StaticMethodReference)
                    {
                        UpdateState(nstate, result, instruction);
                    }
                    // We add result as escaped only if the operand variable is escaped too.
                    else if (operand is IVariable)
                    {
                        var op = operand as IVariable;
                        if (nstate.Escaped(op))
                            UpdateState(nstate, result, instruction);
                    }
                    // Other cases either don't occur (expressions) at this code layer or don't do much.                
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                if (IsWrapAroundCurrent(instruction))
                {
                    SavePreState(instruction, FreshCurrent());
                    var nstate = FreshCurrent();
                    if (nstate.Escaped(instruction.Arguments.ElementAt(0))) UpdateState(nstate, instruction.Result, instruction);
                    SetCurrent(nstate);
                    SavePostState(instruction, FreshCurrent());
                }
                else
                {
                    VisitMethodInvocation(instruction, instruction.Result, instruction.Arguments, instruction.HasResult, instruction.Method.IsStatic);
                }
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                VisitMethodInvocation(instruction, instruction.Result, instruction.Arguments, instruction.HasResult, instruction.Function.IsStatic);
            }

            private void VisitMethodInvocation(Instruction instruction, IVariable result, IList<IVariable> arguments, bool hasResult, bool isStatic)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                var beginIndex = isStatic ? 0 : 1; // to avoid "this".
                for (int i = beginIndex; i < arguments.Count; i++)
                {
                    var v = arguments[i];
                    if (PossiblyRow(v.Type))
                        UpdateState(nstate, v, instruction);
                }

                if (hasResult && PossiblyRow(result.Type))
                {
                    UpdateState(nstate, result, instruction);
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            private bool IsWrapAroundCurrent(MethodCallInstruction instruction)
            {
                var method = instruction.Method;
                //TODO: can this be done better?
                if (method.Name.Value == "get_Current" && instruction.Arguments.Count == 1 && 
                   method.ContainingType.FullName() == "System.Collections.Generic.IEnumerator<ScopeRuntime.Row>") return true;
                return false;
            }


            public override void Visit(CopyMemoryInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                SetCurrent(ScopeEscapeDomain.Top(parent.TrackedFields));
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                SetCurrent(ScopeEscapeDomain.Top(parent.TrackedFields));
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                SetCurrent(ScopeEscapeDomain.Top(parent.TrackedFields));
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(ConvertInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                if (PossiblyRow(instruction.Result.Type))
                {
                    if (nstate.Escaped(instruction.Operand))
                        UpdateState(nstate, instruction.Result, instruction);
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                if (PossiblyRow(instruction.Value.Type))
                    UpdateState(nstate, instruction.Value, instruction);

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            public override void Visit(PhiInstruction instruction)
            {
                SavePreState(instruction, FreshCurrent());
                var nstate = FreshCurrent();

                if (instruction.HasResult && PossiblyRow(instruction.Result.Type))
                {
                    foreach (var v in instruction.Arguments)
                    {
                        if (nstate.Escaped(v))
                        {
                            UpdateState(nstate, instruction.Result, instruction);
                            break;
                        }
                    }
                }

                SetCurrent(nstate);
                SavePostState(instruction, FreshCurrent());
            }

            #endregion


            private IFieldDefinition MatchFieldDefinition(IFieldReference fref)
            {
                foreach (var fdef in parent.TrackedFields)
                {
                    if (fref.Name != fdef.Name) continue;
                    var resolved = fref.Resolve(parent.Host);
                    if (resolved.Equals(fdef)) return fdef;
                }
                return null;
            }

            private bool PossiblyRow(ITypeReference type)
            {
                return parent.PossiblyRow(type);
            }

            public ScopeEscapeDomain State
            {
                get { return currentState; }
                set { currentState = value; }
            }

            private void UpdateState(ScopeEscapeDomain state, IVariable v, Instruction instruction)
            {
                state.SetAllEscaped();
                // TODO: refine the above with the following line of code
                // and aliasing information.
                //state.Escape(fdef);
            }

            private void UpdateState(ScopeEscapeDomain state, IFieldDefinition fdef, Instruction instruction)
            {
                state.SetAllEscaped();
                // TODO: refine the above with the following line of code
                // and aliasing information.
                //state.Escape(fdef);
            }

            private void UpdateStateEscaped(ScopeEscapeDomain state)
            {
                state.SetAllEscaped();
            }

            private void SetCurrent(ScopeEscapeDomain state)
            {
                State = state;
            }

            private ScopeEscapeDomain FreshCurrent()
            {
                return currentState.Clone();
            }

            private void SavePreState(Instruction instruction, ScopeEscapeDomain state)
            {
                preState[instruction] = state;

            }

            private void SavePostState(Instruction instruction, ScopeEscapeDomain state)
            {
                postState[instruction] = state;
            }

            public Dictionary<Instruction, ScopeEscapeDomain> PostStates
            {
                get { return postState; }
            }

            public Dictionary<Instruction, ScopeEscapeDomain> PreStates
            {
                get { return preState; }
            }
        }

    }
}
