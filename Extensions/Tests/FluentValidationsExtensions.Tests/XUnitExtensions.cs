﻿using System;
using System.Linq.Expressions;

namespace MatthiWare.CommandLine.Tests
{
    public static class XUnitExtensions
    {
        public static LambdaExpression CreateLambda<TSource, TProperty>(Expression<Func<TSource, TProperty>> expression)
        {
            return expression;
        }
    }
}
