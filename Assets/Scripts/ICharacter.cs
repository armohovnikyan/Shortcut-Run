using System;
using UnityEngine;


public interface ICharacter
{
  void IsFailing();
  void CheckPlanks(); 

  void ChangeSpeedBonus(float Bonus);
}
