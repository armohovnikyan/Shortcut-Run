using System;
using UnityEngine;


public interface IRunner
{
  void IsFailing();
  void CheckPlanks(); 
  void Jump();
  void Climb();
  void ChangeSpeedBonus(float Bonus);
}
