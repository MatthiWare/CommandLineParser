﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MatthiWare.CommandLine.Abstractions.Validations
{
    public interface IValidatorsContainer
    {
        void AddValidator<TKey>(IValidator<TKey> validator);

        void AddValidator<TKey, V>() where V : IValidator<TKey>;

        bool HasValidatorFor<TKey>();
        bool HasValidatorFor(Type type);

        IValidator<TKey> GetValidatorFor<TKey>();
        IValidator GetValidatorFor(Type key);
    }
}
