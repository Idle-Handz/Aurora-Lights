using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class MonsterElementParser : ElementParser
{
	public override string ParserType => "Monster";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		MonsterElement monsterElement = base.ParseElement(elementNode).Construct<MonsterElement>();
		monsterElement.Size = monsterElement.ElementSetters.GetSetter("size").Value;
		monsterElement.MonsterType = monsterElement.ElementSetters.GetSetter("type").Value;
		monsterElement.Alignment = monsterElement.ElementSetters.GetSetter("alignment").Value;
		monsterElement.Strength = monsterElement.ElementSetters.GetSetter("str").ValueAsInteger();
		monsterElement.Dexterity = monsterElement.ElementSetters.GetSetter("dex").ValueAsInteger();
		monsterElement.Constitution = monsterElement.ElementSetters.GetSetter("con").ValueAsInteger();
		monsterElement.Intelligence = monsterElement.ElementSetters.GetSetter("int").ValueAsInteger();
		monsterElement.Wisdom = monsterElement.ElementSetters.GetSetter("wis").ValueAsInteger();
		monsterElement.Charisma = monsterElement.ElementSetters.GetSetter("cha").ValueAsInteger();
		monsterElement.Ac = monsterElement.ElementSetters.GetSetter("ac").ValueAsInteger();
		monsterElement.Hp = monsterElement.ElementSetters.GetSetter("hp").ValueAsInteger();
		monsterElement.Hd = monsterElement.ElementSetters.GetSetter("hd").Value;
		if (monsterElement.ElementSetters.ContainsSetter("speed"))
		{
			monsterElement.Speed.Base = monsterElement.ElementSetters.GetSetter("speed").ValueAsInteger();
		}
		if (monsterElement.ElementSetters.ContainsSetter("speed:climb"))
		{
			monsterElement.Speed.Climb = monsterElement.ElementSetters.GetSetter("speed:climb").ValueAsInteger();
		}
		if (monsterElement.ElementSetters.ContainsSetter("speed:fly"))
		{
			monsterElement.Speed.Fly = monsterElement.ElementSetters.GetSetter("speed:fly").ValueAsInteger();
		}
		if (monsterElement.ElementSetters.ContainsSetter("speed:swim"))
		{
			monsterElement.Speed.Swim = monsterElement.ElementSetters.GetSetter("speed:swim").ValueAsInteger();
		}
		if (monsterElement.ElementSetters.ContainsSetter("speed:burrow"))
		{
			monsterElement.Speed.Burrow = monsterElement.ElementSetters.GetSetter("speed:burrow").ValueAsInteger();
		}
		return monsterElement;
	}
}
