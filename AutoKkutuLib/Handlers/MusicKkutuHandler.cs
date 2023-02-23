﻿namespace AutoKkutuLib.Handlers;

internal partial class MusicKkutuHandler : AbstractHandler
{
	public override IReadOnlyCollection<Uri> UrlPattern => new Uri[] { new Uri("https://musickkutu.xyz/") };

	public override string HandlerName => "Musickkutu.xyz Handler";

	public MusicKkutuHandler(JsEvaluator jsEvaluator) : base(jsEvaluator)
	{
	}
}
