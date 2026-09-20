using System;
using UnityEngine;


public interface IRunner
{
  void IsFailing();
  void CheckPlanks(); 
  void Jump();
  void Climb(bool Climbing);
  void ChangeSpeedBonus(float Bonus);
}
