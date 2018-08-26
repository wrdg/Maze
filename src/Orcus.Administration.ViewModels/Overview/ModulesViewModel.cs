﻿using MahApps.Metro.IconPacks;
using Orcus.Administration.Library.ViewModels;
using Unclassified.TxLib;

namespace Orcus.Administration.ViewModels.Overview
{
    public class ModulesViewModel : OverviewTabBase
    {
        public ModulesViewModel() : base(Tx.T("Modules"), PackIconFontAwesomeKind.PuzzlePieceSolid)
        {
        }
    }
}