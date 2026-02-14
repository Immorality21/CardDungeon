using UnityEngine;
using UnityEngine.UI;

namespace ImmoralityGaming.Extensions
{

    public static class ImageExtension
	{
		public static void SetAlpha(this Image self, float alpha) {
		    Color tempColor = self.color;
		    tempColor.a = alpha;
		    self.color = tempColor;
	    }

	//	public static void SetColor(this Image self, Color color) {
	//	    Color tempColor = self.color;
	//	    tempColor.a = alpha;
	//	    self.color = tempColor;
	//    }
	}	

}