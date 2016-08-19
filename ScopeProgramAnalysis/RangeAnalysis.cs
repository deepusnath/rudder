﻿using Backend.Analyses;
using Model.ThreeAddressCode.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.Model;
using Model.ThreeAddressCode.Visitor;
using Model.ThreeAddressCode.Instructions;
using Model.Types;
using Model.ThreeAddressCode.Expressions;
using System.Globalization;

namespace ScopeProgramAnalysis
{
    public struct RangeDomain : IAnalysisDomain<RangeDomain>
    {
        public int Start { get; set; }
        public int End { get; set; }

        public bool IsTop
        {
            get
            {
                return Start==int.MinValue && End==int.MaxValue;
            }
        }

        public bool IsBottom
        {
            get
            {
                return Start == 0 && End == -1;
            }
        }


        public RangeDomain(int start, int end)
        {
            this.Start = start;
            this.End = end;
        }
        public RangeDomain Join(RangeDomain oth)
        {
            return new RangeDomain(Math.Min(Start, oth.Start), Math.Max(End, oth.End));
        }
        public RangeDomain Clone()
        {
            return  new RangeDomain(this.Start, this.End);
        }

        public bool LessEqual(RangeDomain oth)
        {
            return this.Start>=oth.Start && oth.End<=this.End;
        }

        public bool Equals(RangeDomain oth)
        {
            return this.Start==oth.Start && this.End==oth.End;
        }

        public RangeDomain Sum(RangeDomain rangeDomain)
        {
            return new RangeDomain(this.Start+rangeDomain.Start,this.End+rangeDomain.End);
        }
        public RangeDomain Sub(RangeDomain rangeDomain)
        {
            return new RangeDomain(this.Start - rangeDomain.Start, this.End - rangeDomain.End);
        }

        public override string ToString()
        {
            if (IsTop) return "_TOP_";
            if(IsBottom) return "_BOTTOM_";
            var result = String.Format(CultureInfo.InvariantCulture, "[{0}..{1}]", Start, End);
            if (Start == End) result = Start.ToString();
            return result;
        }

    }
    public class VariableRangeDomain : IAnalysisDomain<VariableRangeDomain>
    {
        IDictionary<IVariable, RangeDomain> variableRange;
        public VariableRangeDomain()
        {
            this.variableRange = new Dictionary<IVariable, RangeDomain>();
        }
        public bool IsTop
        {
            get
            {
                 return false;
            }
        }

        public VariableRangeDomain Clone()
        {
            var result = new VariableRangeDomain();
            result.variableRange = new Dictionary<IVariable, RangeDomain>(this.variableRange);
            return result;
        }

        public bool Equals(VariableRangeDomain oth)
        {
            return this.LessEqual(oth) && oth.LessEqual(this);
        }

        public VariableRangeDomain Join(VariableRangeDomain right)
        {
            var result = this.Clone();
            foreach(var key in result.variableRange.Keys.ToList())
            {
                if (right.variableRange.ContainsKey(key))
                {
                    result.variableRange[key] = result.variableRange[key].Join(right.variableRange[key]);
                }
            }
            foreach (var k in right.variableRange.Keys.Except(result.variableRange.Keys))
            {
                result.variableRange[k] = right.variableRange[k];
            }
            return result;
        }

        public void AssignValue(IVariable v, RangeDomain value)
        {
            variableRange[v] = value;
        }

        public void AddValue(IVariable v, RangeDomain value)
        {
            if (variableRange.ContainsKey(v))
            {
                variableRange[v] = variableRange[v].Join(value);
            }
            else
            {
                variableRange[v] = value;
            }

        }

        public bool LessEqual(VariableRangeDomain oth)
        {
            var result = this.variableRange.All(kv => oth.variableRange.ContainsKey(kv.Key) && kv.Value.LessEqual(oth.variableRange[kv.Key]));
            return result;
        }

        public void SetValue(IVariable var, RangeDomain value)
        {
            this.variableRange[var] = value;
        }

        public RangeDomain GetValue(IVariable var)
        {
            if(this.variableRange.ContainsKey(var))
                return this.variableRange[var];
            return RangeAnalysis.BOTTOM;
        }
    }


    public class RangeAnalysis: ForwardDataFlowAnalysis<VariableRangeDomain> 
    {
        public static readonly RangeDomain TOP = new RangeDomain(int.MinValue, int.MinValue);
        public static readonly RangeDomain BOTTOM = new RangeDomain(0, -1);

        public DataFlowAnalysisResult<VariableRangeDomain>[] Result { get; private set; }

        public RangeAnalysis(ControlFlowGraph cfg): base(cfg)
        {

        }

        public override DataFlowAnalysisResult<VariableRangeDomain>[] Analyze()
        {
            Result = base.Analyze();
            return Result;
        }

        protected override bool Compare(VariableRangeDomain newState, VariableRangeDomain oldState)
        {
            return newState.LessEqual(oldState);
        }

        protected override VariableRangeDomain Flow(CFGNode node, VariableRangeDomain input)
        {
            var visitor = new RangeAnalysisVisitor(this, input);
            visitor.Visit(node);
            return visitor.State;
        }

        protected override VariableRangeDomain InitialValue(CFGNode node)
        {
            return new VariableRangeDomain();
        }

        protected override VariableRangeDomain Join(VariableRangeDomain left, VariableRangeDomain right)
        {
            return left.Join(right);
        }

        internal class RangeAnalysisVisitor: InstructionVisitor
        {
            private RangeAnalysis rangeAnalysis;

            public RangeAnalysisVisitor(RangeAnalysis rangeAnalysis, VariableRangeDomain oldState)
            {
                this.rangeAnalysis = rangeAnalysis;
                this.State = oldState;
            }

            public VariableRangeDomain State { get; internal set; }

            public override void Visit(LoadInstruction instruction)
            {
                if(instruction.Operand is Constant)
                {
                    var value = ExtractConstant(instruction.Operand as Constant);
                    this.State.SetValue(instruction.Result, value);
                }
                if(instruction.Operand is IVariable)
                {
                    this.State.SetValue(instruction.Result, this.State.GetValue(instruction.Operand as IVariable));
                }
            }

            private RangeDomain ExtractConstant(Constant K)
            {
                int value = -1;
                if (K.Type.Equals(PlatformTypes.Int32))
                {
                    value = (int)K.Value;
                    return new RangeDomain(value, value);
                }
                return RangeAnalysis.BOTTOM;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var op1 = this.State.GetValue(instruction.LeftOperand);
                var op2 = this.State.GetValue(instruction.RightOperand);
                switch(instruction.Operation)
                {
                    case BinaryOperation.Add:
                        this.State.AssignValue(instruction.Result, op1.Sum(op2));
                        break;
                    case BinaryOperation.Sub:
                        this.State.AssignValue(instruction.Result, op1.Sub(op2));
                        break;
                    default:
                        this.State.AssignValue(instruction.Result, RangeAnalysis.TOP);
                        break;
                }
                     
            }

            private IExpression GetExpression(IVariable leftOperand)
            {
                throw new NotImplementedException();
            }

            public override void Default(Instruction instruction)
            {
                foreach (var result in instruction.ModifiedVariables)
                {
                    var range = RangeAnalysis.BOTTOM;
                    foreach (var arg in instruction.UsedVariables)
                    {
                        range = range.Join(this.State.GetValue(arg));
                    }
                    this.State.AssignValue(result, range);
                }
            }
        }
    }
}
